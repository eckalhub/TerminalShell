namespace TerminalShell.Core;

public static class CustomCommandDefaults
{
    public const string Template = """
[F1]
确认,按你的建议来
进度到多少了现在,剩下什么没做
[/F1]

[F2]
codex resume --dangerously-bypass-approvals-and-sandbox
https://platform.openai.com/api-keys
codex logout
npm i -g @openai/codex
codex --dangerously-bypass-approvals-and-sandbox
codex resume --sandbox danger-full-access
codex resume --full-auto
[/F2]
------------------------------------------------------
[F3]
claude --resume --dangerously-skip-permissions
claude --update
claude --resume --permission-mode auto
npm install -g @anthropic-ai/claude-code
[/F3]
------------------------------------------------------
[F4]
npm install -g @google/gemini-cli
gemini -m gemini-3.1-pro-preview
[/F4]

[11]
git init
git add .
git commit -m "完成了XXX功能"
git reset --hard HEAD~1
git checkout -b 新分支名
git checkout main
git merge 新分支名      (合并这个分支)
git branch -D 新分支名  (丢弃这个分支)
[/11]
""";

    public static string GetEffectiveValue(string? configuredValue)
    {
        return string.IsNullOrWhiteSpace(configuredValue)
            ? Template
            : configuredValue;
    }
}
