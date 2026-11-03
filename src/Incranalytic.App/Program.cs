namespace Incranalytic.App;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var form = new StudioForm();
        if (args.Length == 2 && args[0] == "--verify-studio")
            form.Shown += (_, _) => form.BeginInvoke(new Action(() =>
            {
                try { form.VerifyStudio(args[1]); }
                catch (Exception ex) { File.WriteAllText(args[1], "FAIL: " + ex); Environment.ExitCode = 1; }
                finally { form.Close(); }
            }));
        Application.Run(form);
    }
}
