namespace TerminalShell.Core;

public static class TaskAlertDefaults
{
    public const string WaitForUserInputKeywords = """
[Y/n]
[y/N]
yes/no
please confirm
do you want me
should i
which one
which option
pick one
let me know your choice
press enter
press enter to confirm
esc to go back
implement this plan
stay in plan mode
waiting for your input
请确认
是否继续
按回车确认
继续吗
请选择
需要你确认
需要你选择
选哪个
""";

    public const string FailureKeywords = """
You've hit your usage limit
usage limit
rate limit
quota exceeded
too many requests
429 too many requests
exceeded retry limit
try again later
server overloaded
service unavailable
Selected model is at capacity
Please try a different model
authentication failed
invalid api key
permission denied
insufficient credits
billing hard limit
""";
}
