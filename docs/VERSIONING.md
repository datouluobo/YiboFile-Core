# 版本号规范

## 版本号格式
版本号遵循 `Major.Minor.Build.Revision` 格式。

## Revision (第4位) 说明
最后一位数字（Revision）用于区分程序版本：
- **0**: Core (Free / 核心版)
- **1**: Pro (专业版)
- **2**: Ultra (终极版)

## 发布规则
- 在发布标签（Tag）时，通常忽略第4位的 `0`。
- 例如：Core 版的内部版本号为 `1.0.2.0`，发布的 GitHub 标签为 `v1.0.2`。
