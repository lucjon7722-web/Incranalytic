namespace Incranalytic.Installer;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        bool verify = args.Length == 2 && args[0] == "--verify-manager";
        var form = new ManagerForm(verify);
        if (verify) form.Shown += (_, _) => form.BeginInvoke(new Action(() =>
        {
            try { form.Verify(args[1]); }
            catch (Exception ex) { File.WriteAllText(args[1], "FAIL: " + ex); Environment.ExitCode = 1; }
            finally { form.Close(); }
        }));
        Application.Run(form);
    }
}
