using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Incranalytic.Installer;

public static class TokenStore
{
    public const string Target = "Incranalytic/GitHubReleases";
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct Credential
    {
        public uint Flags, Type;
        [MarshalAs(UnmanagedType.LPWStr)] public string TargetName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint BlobSize;
        public IntPtr Blob;
        public uint Persist, AttributeCount;
        public IntPtr Attributes;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Alias;
        [MarshalAs(UnmanagedType.LPWStr)] public string? UserName;
    }
    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool Write(ref Credential credential, uint flags);
    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool Read(string target, uint type, uint flags, out IntPtr credential);
    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool Delete(string target, uint type, uint flags);
    [DllImport("advapi32.dll")] static extern void CredFree(IntPtr credential);
    public static string? Load(string target = Target)
    {
        if (!Read(target, 1, 0, out var pointer)) { int code = Marshal.GetLastWin32Error(); if (code == 1168) return null; throw new Win32Exception(code); }
        try { var c = Marshal.PtrToStructure<Credential>(pointer); return Marshal.PtrToStringUni(c.Blob, checked((int)c.BlobSize / 2)); }
        finally { CredFree(pointer); }
    }
    public static void Save(string token, string target = Target)
    {
        if (token.Length > 1280) throw new ArgumentException("Token exceeds Windows credential storage limit.");
        var pointer = Marshal.StringToCoTaskMemUni(token);
        try
        {
            var credential = new Credential { Type = 1, TargetName = target, UserName = "GitHub", Blob = pointer, BlobSize = checked((uint)token.Length * 2), Persist = 2 };
            if (!Write(ref credential, 0)) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        finally { for (int i = 0; i < token.Length * 2; i++) Marshal.WriteByte(pointer, i, 0); Marshal.FreeCoTaskMem(pointer); }
    }
    public static void Forget(string target = Target) { if (!Delete(target, 1, 0)) { int code = Marshal.GetLastWin32Error(); if (code != 1168) throw new Win32Exception(code); } }
}
