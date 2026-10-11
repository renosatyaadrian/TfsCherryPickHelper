using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace TfsCherryPickHelper.Services;

/// <summary>
/// Stores the TFS password in the Windows Credential Manager (DPAPI-encrypted,
/// per-user) instead of a plaintext file. Same mechanism used by
/// git-credential-manager and browsers.
/// </summary>
public static class CredentialManagerService
{
    private const string TargetName = "TfsCherryPickHelper";
    private const int CredTypeGeneric = 1;
    private const int CredPersistLocalMachine = 2;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL
    {
        public int Flags;
        public int Type;
        public string TargetName;
        public string Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public int CredentialBlobSize;
        public IntPtr CredentialBlob;
        public int Persist;
        public int AttributeCount;
        public IntPtr Attributes;
        public string TargetAlias;
        public string UserName;
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite(ref CREDENTIAL credential, int flags);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, int type, int flags, out IntPtr credentialPtr);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, int type, int flags);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr credential);

    /// <summary>
    /// Writes the password to the Windows Credential Manager.
    /// An empty password deletes the stored credential.
    /// Returns true on success.
    /// </summary>
    public static bool WritePassword(string password)
    {
        if (string.IsNullOrEmpty(password))
            return DeletePassword();

        var blob = Encoding.UTF8.GetBytes(password);
        var blobPtr = Marshal.AllocHGlobal(blob.Length);
        try
        {
            Marshal.Copy(blob, 0, blobPtr, blob.Length);
            var credential = new CREDENTIAL
            {
                Type = CredTypeGeneric,
                TargetName = TargetName,
                CredentialBlobSize = blob.Length,
                CredentialBlob = blobPtr,
                Persist = CredPersistLocalMachine,
                UserName = Environment.UserName
            };

            if (!CredWrite(ref credential, 0))
                return false;

            return true;
        }
        finally
        {
            Marshal.FreeHGlobal(blobPtr);
        }
    }

    /// <summary>
    /// Reads the password from the Windows Credential Manager,
    /// or null when no credential is stored.
    /// </summary>
    public static string? ReadPassword()
    {
        if (!CredRead(TargetName, CredTypeGeneric, 0, out var credentialPtr))
            return null;

        try
        {
            var credential = Marshal.PtrToStructure<CREDENTIAL>(credentialPtr);
            if (credential.CredentialBlobSize == 0 || credential.CredentialBlob == IntPtr.Zero)
                return null;

            var blob = new byte[credential.CredentialBlobSize];
            Marshal.Copy(credential.CredentialBlob, blob, 0, blob.Length);
            return Encoding.UTF8.GetString(blob);
        }
        finally
        {
            CredFree(credentialPtr);
        }
    }

    /// <summary>
    /// Deletes the stored credential. Returns true when deleted
    /// or when no credential existed.
    /// </summary>
    public static bool DeletePassword()
    {
        if (!CredDelete(TargetName, CredTypeGeneric, 0))
        {
            // ERROR_NOT_FOUND means there was nothing to delete - that is fine.
            const int errorNotFound = 1168;
            return Marshal.GetLastWin32Error() == errorNotFound;
        }

        return true;
    }
}
