using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using static NLInfoBar.NativeMethods;

namespace NLInfoBar.Api
{
    /// <summary>Authenticode checks run on each NetLimiter API file before it is loaded (docs/DESIGN.md).</summary>
    internal static class Authenticode
    {
        private static readonly Guid GenericVerifyV2 = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

        /// <summary>
        /// Verifies the file's embedded signature and its chain to a trusted root. Never touches the network:
        /// no revocation check and URL retrieval from the local cache only. Returns 0 when trusted.
        /// </summary>
        public static int Verify(string path)
        {
            var fileInfo = new WINTRUST_FILE_INFO
            {
                cbStruct = (uint)Marshal.SizeOf(typeof(WINTRUST_FILE_INFO)),
                pcwszFilePath = path,
            };
            IntPtr pFile = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WINTRUST_FILE_INFO)));
            bool marshaled = false;
            try
            {
                Marshal.StructureToPtr(fileInfo, pFile, false);
                marshaled = true;

                var data = new WINTRUST_DATA
                {
                    cbStruct = (uint)Marshal.SizeOf(typeof(WINTRUST_DATA)),
                    dwUIChoice = WTD_UI_NONE,
                    fdwRevocationChecks = WTD_REVOKE_NONE,
                    dwUnionChoice = WTD_CHOICE_FILE,
                    pFile = pFile,
                    dwStateAction = WTD_STATEACTION_VERIFY,
                    dwProvFlags = WTD_REVOCATION_CHECK_NONE | WTD_CACHE_ONLY_URL_RETRIEVAL,
                };
                int result = WinVerifyTrust(INVALID_HANDLE_VALUE, GenericVerifyV2, ref data);

                data.dwStateAction = WTD_STATEACTION_CLOSE;
                WinVerifyTrust(INVALID_HANDLE_VALUE, GenericVerifyV2, ref data);
                return result;
            }
            finally
            {
                if (marshaled)
                    Marshal.DestroyStructure(pFile, typeof(WINTRUST_FILE_INFO));
                Marshal.FreeHGlobal(pFile);
            }
        }

        /// <summary>Organization (O=) of the certificate that signed the file, or null.</summary>
        public static string GetSignerOrganization(string path)
        {
            try
            {
                using (X509Certificate signer = X509Certificate.CreateFromSignedFile(path))
                using (var cert = new X509Certificate2(signer))
                {
                    string dn = cert.SubjectName.Decode(
                        X500DistinguishedNameFlags.UseNewLines | X500DistinguishedNameFlags.DoNotUseQuotes);
                    foreach (string part in dn.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (part.StartsWith("O=", StringComparison.Ordinal))
                            return part.Substring(2).Trim();
                    }
                }
            }
            catch (CryptographicException)
            {
                // Unsigned or unreadable signature.
            }
            return null;
        }
    }
}
