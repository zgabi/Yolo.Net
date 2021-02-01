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
        public static Version CudaVersion = new Version(10, 2);

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

            const string yoloDllFileName = "yolo_cpp_dll_gpu.dll";
            string cudnnFileName = "cudnn64_7.dll";
            if (File.Exists(yoloDllFileName))
            {
                report.YoloGpuDll = true;
                var data = File.ReadAllBytes(yoloDllFileName);
                for (int i = 0; i < data.Length - 24; i++)
                {
                    if (data[i] != 'c') continue;
                    if (data[i + 1] != 'u') continue;
                    if (data[i + 2] != 'd') continue;
                    if (data[i + 3] != 'n') continue;
                    if (data[i + 4] != 'n') continue;
                    if (data[i + 5] != '6') continue;
                    if (data[i + 6] != '4') continue;
                    if (data[i + 7] != '_') continue;

                    int j = i + 8;
                    int version = 0;
                    int digits = 0;
                    bool ok = true;
                    while (true)
                    {
                        int ch = data[j];
                        if (ch == '.' || digits > 9)
                        {
                            break;
                        }

                        if (ch < '0' || ch > '9')
                        {
                            ok = false;
                            break;
                        }

                        version *= 10;
                        version += ch - '0';
                        digits++;
                        j++;
                    }

                    if (!ok) continue;
                    if (data[j + 1] != 'd') continue;
                    if (data[j + 2] != 'l') continue;
                    if (data[j + 3] != 'l') continue;
                    if (data[j + 4] != 0) continue;

                    cudnnFileName = $"cudnn64_{version}.dll";
                    break;
                }
            }

            if (File.Exists(cudnnFileName))
            {
                report.CudnnExists = true;
            }

            var environmentVariables = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Machine);
            if (environmentVariables.Contains("CUDA_PATH"))
            {
                report.CudaExists = true;
            }

            if (environmentVariables.Contains($"CUDA_PATH_V{CudaVersion.Major}_{CudaVersion.Minor}"))
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
