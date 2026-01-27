using System.Runtime.InteropServices;
using System.Text;

namespace SSH_Helper.Services
{
    /// <summary>
    /// Windows Credential Manager provider.
    /// </summary>
    public sealed class CredentialManagerProvider : ICredentialProvider
    {
        private const uint CRED_TYPE_GENERIC = 1;
        private const uint CRED_PERSIST_LOCAL_MACHINE = 2;

        public bool IsAvailable => OperatingSystem.IsWindows();

        public bool TryGetPassword(string target, out string username, out string password)
        {
            username = string.Empty;
            password = string.Empty;

            if (!IsAvailable || string.IsNullOrWhiteSpace(target))
                return false;

            if (!CredRead(target, CRED_TYPE_GENERIC, 0, out var credentialPtr) || credentialPtr == IntPtr.Zero)
                return false;

            try
            {
                var credential = Marshal.PtrToStructure<CREDENTIAL>(credentialPtr);
                username = credential.UserName ?? string.Empty;

                if (credential.CredentialBlobSize > 0 && credential.CredentialBlob != IntPtr.Zero)
                {
                    password = Marshal.PtrToStringUni(credential.CredentialBlob, (int)credential.CredentialBlobSize / 2) ?? string.Empty;
                }

                return true;
            }
            finally
            {
                CredFree(credentialPtr);
            }
        }

        public bool SavePassword(string target, string username, string password, string? comment = null)
        {
            if (!IsAvailable || string.IsNullOrWhiteSpace(target))
                return false;

            var credential = new CREDENTIAL
            {
                Type = CRED_TYPE_GENERIC,
                TargetName = target,
                UserName = username ?? string.Empty,
                Comment = comment ?? string.Empty,
                Persist = CRED_PERSIST_LOCAL_MACHINE
            };

            var passwordBytes = Encoding.Unicode.GetBytes(password ?? string.Empty);
            credential.CredentialBlobSize = (uint)passwordBytes.Length;
            credential.CredentialBlob = Marshal.AllocCoTaskMem(passwordBytes.Length);

            try
            {
                Marshal.Copy(passwordBytes, 0, credential.CredentialBlob, passwordBytes.Length);
                return CredWrite(ref credential, 0);
            }
            finally
            {
                if (credential.CredentialBlob != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(credential.CredentialBlob);
                }
            }
        }

        public bool DeletePassword(string target)
        {
            if (!IsAvailable || string.IsNullOrWhiteSpace(target))
                return false;

            return CredDelete(target, CRED_TYPE_GENERIC, 0);
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct CREDENTIAL
        {
            public uint Flags;
            public uint Type;
            public string TargetName;
            public string Comment;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
            public uint CredentialBlobSize;
            public IntPtr CredentialBlob;
            public uint Persist;
            public uint AttributeCount;
            public IntPtr Attributes;
            public string TargetAlias;
            public string UserName;
        }

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CredWrite([In] ref CREDENTIAL userCredential, [In] uint flags);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CredRead(string target, uint type, uint reservedFlag, out IntPtr credentialPtr);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CredDelete(string target, uint type, uint flags);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern void CredFree([In] IntPtr cred);
    }
}
