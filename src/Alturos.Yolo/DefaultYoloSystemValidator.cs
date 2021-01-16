using Alturos.Yolo.Model;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Alturos.Yolo
{
    public class DefaultYoloSystemValidator : IYoloSystemValidator
    {
        public SystemValidationReport Validate()
        {
            var report = new SystemValidationReport();

#if NETSTANDARD

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                report.MicrosoftVisualCPlusPlusRedistributableExists = IsMicrosoftVisualCPlusPlus2017Available();
            }
            else
            {
                report.MicrosoftVisualCPlusPlusRedistributableExists = true;
            }

#endif

#if NET461
            report.MicrosoftVisualCPlusPlusRedistributableExists = this.IsMicrosoftVisualCPlusPlus2017Available();
#endif

            if (File.Exists("cudnn64_7.dll"))
            {
                report.CudnnExists = true;
            }

            var environmentVariables = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Machine);
            if (environmentVariables.Contains("CUDA_PATH"))
            {
                report.CudaExists = true;
            }

            if (environmentVariables.Contains("CUDA_PATH_V10_2"))
            {
                report.CudaExists = true;
            }

            return report;
        }

        private bool IsMicrosoftVisualCPlusPlus2017Available()
        {
            //Detect if Visual C++ Redistributable for Visual Studio is installed
            //https://stackoverflow.com/questions/12206314/detect-if-visual-c-redistributable-for-visual-studio-2012-is-installed/
            const string subKeyOld = ",,amd64,14.0,bundle";
            var regexSubKeyName = new Regex(@"VC,redist.x64,amd64,(?<versionMajor>\d+)\.(?<versionMinor>\d+),bundle", RegexOptions.Compiled);
            var regexDisplayName = new Regex(@"Microsoft Visual C\+\+ [\d-]+ Redistributable \(x64\).*", RegexOptions.Compiled | RegexOptions.IgnoreCase);

            using (var registryKey = Registry.ClassesRoot.OpenSubKey(@"Installer\Dependencies", false))
            {
                if (registryKey == null)
                {
                    return false;
                }

                foreach (var subKeyName in registryKey.GetSubKeyNames())
                {
                    int versionMajor = 0;
                    int versionMinor = 0;
                    if (subKeyName == subKeyOld)
                    {
                        versionMajor = 14;
                        versionMinor = 0;
                    }
                    else
                    {
                        var match = regexSubKeyName.Match(subKeyName);
                        if (match.Success)
                        {
                            int.TryParse(match.Groups["versionMajor"].Value, out versionMajor);
                            int.TryParse(match.Groups["versionMinor"].Value, out versionMinor);
                        }
                    }

                    // accept 14.0 or 14.16+
                    if (versionMajor > 14 || (versionMajor == 14 && (versionMinor == 0 || versionMinor >= 16)))
                    {
                        var subKey = registryKey.OpenSubKey(subKeyName, false);
                        if (subKey == null)
                        {
                            continue;
                        }

                        var displayName = subKey.GetValue("DisplayName") as string;
                        if (string.IsNullOrEmpty(displayName))
                        {
                            continue;
                        }

                        if (regexDisplayName.IsMatch(displayName))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
