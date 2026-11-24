namespace MyMorningNews.Core.Integrations.Implementations
{
    using System.Security.Cryptography.X509Certificates;

    internal static class CertificateLoader
    {
        internal static X509Certificate2 Load(byte[] data)
        {
#if NET9_0_OR_GREATER
            return X509CertificateLoader.LoadCertificate(data);
#else
            return new X509Certificate2(data);
#endif
        }
    }
}
