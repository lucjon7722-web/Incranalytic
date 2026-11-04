namespace Incranalytic.App;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        bool verify = args.Length == 2 && args[0] == "--verify-studio";
        var profile = verify ? new Incranalytic.Core.StudioProfile(Path.Combine(Path.GetTempPath(), "Incranalytic-UiProfile-" + Guid.NewGuid().ToString("N"))) : null;
        var form = new StudioForm(profile, interactiveStartup: !verify);
        if (verify)
            form.Shown += (_, _) => form.BeginInvoke(new Action(() =>
            {
                try { form.VerifyStudio(args[1]); }
                catch (Exception ex) { File.WriteAllText(args[1], "FAIL: " + ex); Environment.ExitCode = 1; }
                finally { form.Close(); }
            }));
        Application.Run(form);
    }
}
