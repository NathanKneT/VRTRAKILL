// Left out of the Thunderstore build (THUNDERSTORE, tools/package-thunderstore.ps1): mod managers update the mod.
#if !THUNDERSTORE
namespace VRTRAKILL.Updates
{
    // The public half of the release signing key (RSA 3072); tools/sign-release.ps1 -Init writes this file.
    internal static class ReleaseKey
    {
        public const string ModulusBase64 = "9dul4WhYMQ7VoX+TbquKrk+zXge9KSU/2flERcHDNW0AE5i+JJSt+Iovc/f3KB2t9TpHUEn0hlypFU3YdDq4xFCJHCxLwFnpdFIifGEErAnxq+RYDm1dyiDb0fCETAjI1sWjVAkDKHqqCo38Wq1PKc56G6MppN3fkrzBFuPai2q/irtmXSFGcX30TXDkAQZ9tJNyvftRwXMGvkKRYO24mepzQAgabEsJ5EQRTnr2jgre1xjRb28HT5xi7mD4HbkYGs+TgktYbQfZFOsXsfgy+phAgBVOxTo7mS/QjDSgA2CoVPCUUHbR2rCRTvw7NFdRUQz+QvBfd/reP4zTv4zqXeD38+1jeltqE+rKXSesQ9ETaYPar46N+SibOVnXdxfcCmbg82QZHcfXbwStFk4FEEf4xUySAVU5o6wgpBRuoyuWr2rH14sH+m5Prs38lTmIj/A3jbjNMq1U2l46yEhUmY9jFmXTijnREoOgJHzy3ojrQq9YmWLptaKYubwyB2nZ";
        public const string ExponentBase64 = "AQAB";
    }
}
#endif
