<!-- project-governance:start -->
# 项目治理

本区块是本项目目录、提交和发布政策的集中入口。主 agent 每次任务开始读取，向子 agent 传递适用规则。用户明确指示与环境权限优先；已有授权在约定范围内持续有效。普通任务遵守当前结构，迁移只在首次接入或显式治理时执行。

## 项目设置

接入时依据仓库事实填写以下值，保留本区块外已有规则，并协调子目录规则；不得留下未解析变量。

| 设置 | 本项目值 |
| --- | --- |
| 项目名称 | MyFences |
| 必需额外目录/根部文件及原因 | 根部 README.md、README.en.md、LICENSE、THIRD_PARTY.md 为入口和许可；AGENTS.md、.gitignore、Directory.Build.props、MyFences.slnx 为治理及构建配置。.tools/ 保留本地 SDK 与 NuGet 缓存，.local/ 保留诊断和构建缓存，均忽略。研究项目的上游克隆保留在本地并忽略，仅提交来源说明。 |
| 版本来源、格式和既有自动机制 | src/MyFences.App/MyFences.App.csproj 的 Version，SemVer；无自动升版。初始 Git 交付为 0.1.2；本次新功能交付为 0.2.0。 |
| 适用验证命令及工作目录 | 仓库根目录：dotnet build MyFences.slnx -c Release；dotnet test tests/MyFences.Tests -c Release。无全局 SDK 时使用 .tools/dotnet/dotnet.exe。涉及桌面互操作时运行 WindowsChecks，诊断使用独立 .local 数据目录。 |
| 发行构建命令及工作目录 | 仓库根目录：scripts/publish.ps1；默认使用本地 SDK，自包含 win-x64 单文件发布。开发输出位于 .local/publish-v<version>。首层菜单另需 llvm-mingw x64、Windows SDK MakeAppx/SignTool、本地签名 PFX；可传 -NativeCompiler、-WindowsSdkBin、-SigningCertificate，默认路径见 scripts/build-desktop-menu.ps1。私钥仅在忽略的 .local 中，交付只附公开 CER。 |
| 发行打包命令、交付文件；没有则源码 ZIP | 仓库根目录：scripts/release.ps1；从 HEAD 导出干净源码，生成 release/v<version>/ 中的便携 ZIP、源码 ZIP、CHANGELOG.md 和 release.json（完整 commit、UTC 时间及 SHA-256）。 |
| 已配置远端、仓库和目标分支；没有则未配置 | origin = https://github.com/t66sun/myFences.git，main。用户已授权 0.2.0 的 commit、push、对应 tag、GitHub Release 和交付附件上传；后续版本依据当次授权。 |

## 目录与接入

六目录必须存在，没有可跟踪内容的目录用 .gitkeep 入 Git；仅有被忽略产物时仍保留占位文件，特别是 release/.gitkeep：

| 路径 | 内容 |
| --- | --- |
| src/ | 正式实现及运行资源 |
| scripts/ | 开发、构建、维护和发布脚本 |
| tests/ | 测试及 fixtures |
| docs/ | 稳定使用、维护和设计文档 |
| research/ | 按主题组织的研究结论、比较、实验与原型 |
| release/ | 本地版本交付包 |

研究生成物放 research/<主题>/artifacts/。正式内容、研究结论和实验代码入 Git；生成 artifacts 和 release 包默认忽略，保留 release/.gitkeep 可跟踪。正式内容也包括必要配置、项目政策和结构。工具必需的额外路径保留并记录为例外。

接入/治理时盘点再迁移，同步引用并验证；清理确认可再生成的输出，不删除来源不明或唯一内容。保留用户已有修改、配置和无关规则。非 Git 项目接入时初始化本地 Git；不自动配置远端或修改全局身份，提交身份不明时核实。已有被跟踪生成物仅核实后从索引移除，保留需留在本地的文件。

## 任务、版本与提交

1. 主 agent 负责集成、最终验证、版本更新、commit、打包及已授权远端操作。子 agent 只改分配文件并交付 diff/产物与检查证据，不 commit、升版、创建 tag 或操作远端。
2. 开始时记录当前分支、HEAD、暂存/未暂存 diff 和未跟踪文件。沿用当前分支，只提交本任务改动，保持无关改动及原暂存状态。不要用全量 add 或普通 commit 混入原已暂存内容；同文件混合修改要按开始时快照分离补丁，使用临时索引提交并恢复原暂存差异。迁移提交须包含旧路径删除和新路径新增/修改，提交前核对候选树，提交后检查旧路径从 commit 移除；不能只选择 rename 中的新路径。归属不能可靠分离时核实具体冲突。
3. 纯问答、只读、无可跟踪改动不 commit、不升版、不打包。research-only 任务验证后自动 commit，不升版。正式内容变化才自动升版并交付本地包。
4. 沿用上述版本来源/机制；缺少来源时根部 VERSION 存不带 v 的 SemVer。无既有版本的新项目首版 v0.1.0；本项目沿用上述既有版本。无既有自动机制时，破坏性变更 Major、新功能 Minor，修复/正式文档/测试/配置/结构整理 Patch。仅发布既有版本不升版；pending 版本修复重试不递增。
5. 版本更新及必要 lock/元数据进入同一任务提交。在最终适用验证及发行构建通过后自动提交；失败保留工作内容和本任务候选版本并修复，不自动 commit、不宣称完成。重试时从最近已提交版本及该任务的实际变化重新确认候选，不将未提交候选当成已完成版本再递增；归属不明先核实。重试后若无正式变化，撤去本任务自己的候选版本更新，不制造空发布。使用现有提交风格，否则 Conventional Commits。

## 本地交付

- 正式提交完成后立即为 release/<tag>/ 写 pending 状态的 release.json，记录版本、tag、完整 commit、created_at（UTC ISO 8601）、completed_at=null、files=[]、error=null；随后生成包。此状态用于跨任务识别尚未完成的版本。
- 从记录 commit 的干净内容导出/构建，不打包含无关修改的工作区，不切换当前分支。优先生成配置的发行产物，否则从 commit 制作正式源码 ZIP，包含 docs/scripts/src/tests 和必要配置，排除 release/、research/、.git 与未提交文件。
- 附 CHANGELOG.md：版本、commit、正式变化和实际验证结果。验证包非空、交付清单正确、ZIP 可读取且内容来自记录 commit；对交付文件（含 CHANGELOG.md，不含 release.json 自身）计算 SHA-256。
- release.json 的 files 为 [{"path":"相对版本目录的文件名","sha256":"实际校验值"}]。最终验证成功才原子写入 status=complete、completed_at=UTC ISO 8601、error=null。
- 打包/校验失败保持 pending，写 error，保留 commit。必要修复另作提交但版本不递增，更新记录的 commit，重建受影响包并验证。pending 不得发布。
- 成功后按 completed_at 仅保留最近三个 complete 版本。只清理 release.json 识别的本项目生成版本，并核实目标实际位于本项目 release 内；保留 .gitkeep、pending 和不明内容。失败不触发成功包清理，GitHub Release 不受本地清理影响。
- 本地交付不自动创建 Git tag。

## 远端授权与发布

无明确远端授权只做本地流程。已授予的授权在项目/版本/目标范围内有效，不重复询问。

| 授权 | 动作范围 |
| --- | --- |
| push | 仅向明确目标推送相关提交；无 tag、Release 或附件上传 |
| 发布到 GitHub | 所需 push、对应 tag、GitHub Release 和交付附件上传 |

选择明确指定的 complete 版本；未指定时采用最新成功本地版并说明版本、commit、目标和附件。远端或分支不能唯一确定时核实。发布前重验状态和 SHA-256，tag 必须指向 release.json 记录的 commit，相关提交必须可达目标分支。附件为 files 列出的交付文件加 release.json，后者提供 commit 和校验清单且不纳入自身哈希列表。

同名远端 tag 指向不同 commit，或已有 Release/附件冲突时停止对应步骤并报告，不 force-push、不覆盖或删除已发布内容。重试可核实并复用完全一致的步骤，只补缺失内容；部分失败记录已完成与待完成动作，不回滚远端、不重复升版。结束时检查远端 tag、Release 和附件，并返回实际链接。

## 完成证据

报告适用验证、实际 commit、版本和本地包位置；发布时附 Release 链接。失败报告停点及已发生动作；研究任务说明未升版，pending 不算成功交付。
<!-- project-governance:end -->
