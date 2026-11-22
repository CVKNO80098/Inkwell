# 自动化构建与代码签名

仓库包含两个 GitHub Actions 工作流：

| 工作流 | 触发 | 作用 |
| --- | --- | --- |
| `.github/workflows/build.yml` | `push`、`pull_request`、手动 | 用 MSBuild 构建 `Release`，跑 13 组解析器测试，上传未签名 zip |
| `.github/workflows/release.yml` | `v*` 标签、手动 | 构建 + 测试 → 提交 SignPath 签名 → 校验签名 → 打包 zip → 创建 GitHub Release |

签名使用 [SignPath](https://signpath.io/) 的 Open Source Code Signing（对公开仓库免费，需要申请并通过审核）。它签发的是**受信任的 CA 证书**，因此签名后的 exe 在 Windows 上会显示真实的发布者，而不是“未知发布者”。

> 私钥由 SignPath 的 HSM 保管，仓库和 GitHub Secrets 里都不需要、也不应该出现 `.pfx` 或私钥。

## 一、在 SignPath 创建项目

1. 用公开仓库申请 SignPath Foundation（<https://signpath.org/>），安装 [SignPath GitHub App](https://github.com/apps/signpath) 并授权本仓库。
2. 在 SignPath 中创建项目，slug 设为 `inkwell`（或自定义，后面用变量覆盖）。
3. 创建 **Artifact Configuration**，slug 设为 `inkwell-exe`，内容使用仓库里的
   [`.signpath/artifact-configuration.xml`](../.signpath/artifact-configuration.xml)。
   该配置的根节点是单个 `<pe-file>`，与工作流上传的裸 PE 文件对应。
4. 创建 **Signing Policy**，slug 设为 `release-signing`。建议只允许 `refs/tags/v*` 的构建，并把审批设为手动/自动按你的需要。
5. 创建一个有提交签名请求权限的 **API Token**；同时记下 **Organization ID**。

## 二、配置 GitHub

在仓库 `Settings → Secrets and variables → Actions` 中添加：

**Secrets（必填）**

- `SIGNPATH_API_TOKEN`：上一步的 API Token
- `SIGNPATH_ORGANIZATION_ID`：SignPath Organization ID

**Variables（可选，覆盖默认 slug）**

- `SIGNPATH_PROJECT_SLUG`，默认 `inkwell`
- `SIGNPATH_SIGNING_POLICY_SLUG`，默认 `release-signing`
- `SIGNPATH_ARTIFACT_CONFIGURATION_SLUG`，默认 `inkwell-exe`

同时确认 `Settings → Actions → General → Workflow permissions` 允许 workflow 写 Release（工作流已声明 `contents: write`，若仓库策略更严格需要在仓库设置里放开）。

## 三、发版

1. 修改 `src/App/App.cs` 里的 `Version`。
2. 提交并打与版本一致的标签：

   ```powershell
   git tag v1.0.7
   git push origin v1.0.7
   ```

3. `release.yml` 会：
   - 用 `msbuild Inkwell.sln /p:Configuration=Release` 构建；
   - 运行 `build\vs-tests\Tests.exe`；
   - 校验标签版本与 `App.Version` 一致；
   - 把未签名的 `Inkwell.exe` 以 `archive: false` 上传为 GitHub 制品；
   - 调用 `signpath/github-action-submit-signing-request@v3` 提交签名，等待完成；
   - 用签名后的 exe 替换，校验 `Get-AuthenticodeSignature` 为 `Valid`；
   - 打包 `Inkwell-<version>.zip` + `sha256.txt`，创建 GitHub Release。

也可以在 Actions 页面手动运行 `Release`。手动运行时可以取消 `sign` 输入来跳过签名（只适合测试，正式发布请保持勾选）。

## 四、工作流里的关键约定

- **单文件制品**：`actions/upload-artifact@v4` 使用 `archive: false`，SignPath 收到的是裸 PE 文件；对应签名步骤使用 `skip-decompress: true`，制品配置根节点是 `<pe-file>`。
- **GitHub 托管 runner**：SignPath 的 OSS 策略要求参与签名的所有 job 都跑在 GitHub 托管的 runner 上，所以工作流固定使用 `windows-2022`，不要改成 self-hosted。
- **版本一致性**：标签 `v1.0.7` 必须与 `App.Version` 完全一致，否则工作流会直接失败，避免发布版本号与程序内版本号不符。
- **签名校验**：只有 `Get-AuthenticodeSignature` 返回 `Valid` 才会继续打包；如果返回 `UnknownError`/`NotTrusted`，说明证书链或时间戳有问题，工作流会失败。

## 五、常见问题

| 现象 | 原因 / 处理 |
| --- | --- |
| `github-artifact-id` 为空 | 确认使用 `actions/upload-artifact@v4` 或更高，并且上传步骤在签名步骤之前 |
| 签名步骤被跳过 | 缺少 `SIGNPATH_API_TOKEN` / `SIGNPATH_ORGANIZATION_ID`，或手动运行时 `sign` 未勾选 |
| SignPath 返回 artifact 不匹配 | 检查制品配置根节点是否为 `<pe-file>`，以及上传的是否是单个 exe |
| 标签发布报缺少 secrets | 这是有意的：正式标签发布必须配置 SignPath，避免误发未签名版本 |
| 签名后仍被 SmartScreen 拦截 | 新证书需要积累信誉；EV 证书可立即建立信誉，OV/OSS 证书通常需要一段时间的下载量。签名本身已能让“发布者”显示为 SignPath 的证书主体 |

更多参考：[SignPath GitHub 集成文档](https://docs.signpath.io/trusted-build-systems/github)、[artifact configuration 示例](https://docs.signpath.io/artifact-configuration/examples)。
