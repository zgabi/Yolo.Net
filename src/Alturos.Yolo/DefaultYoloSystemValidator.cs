using Alturos.Yolo.Model;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Alturos.Yolo
{
    public class DefaultYoloSystemValidator : IYoloSystemValidator
    {
        public static byte[] CudnnPattern = Encoding.ASCII.GetBytes("cudnn64_*.dll\0");
        public static byte[] CudartPattern = Encoding.ASCII.GetBytes("cudart64_*.dll\0");

        private int _cudartVersion;
        private const int CudartVersionForAmpere = 110;

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
            int cudnnVersion = 7;
            int cudartVersion = 102; // 10.2

            if (File.Exists(yoloDllFileName))
            {
                report.YoloGpuDll = true;
                var data = File.ReadAllBytes(yoloDllFileName);
                if (FindVersion(data, CudnnPattern, out int version))
                {
                    cudnnVersion = version;
                }

                if (FindVersion(data, CudartPattern, out version))
                {
                    cudartVersion = version;
                    _cudartVersion = version;
                }
            }

            string cudnnFileName = $"cudnn64_{cudnnVersion}.dll";
            if (File.Exists(cudnnFileName))
            {
                report.CudnnExists = true;
            }

            var environmentVariables = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Machine);
            if (environmentVariables.Contains("CUDA_PATH"))
            {
                report.CudaExists = true;
            }

            int cudaVersionMajor = cudartVersion / 10;
            int cudaVersionMinor = cudartVersion % 10;
            if (environmentVariables.Contains($"CUDA_PATH_V{cudaVersionMajor}_{cudaVersionMinor}"))
            {
                report.CudaExists = true;
            }

            return report;
        }

        public bool IsCudaVersion110()
        {
            return _cudartVersion == CudartVersionForAmpere;
        }

        private bool FindVersion(byte[] data, byte[] pattern, out int version)
        {
            int starIdx = -1;
            for (int i = 0; i < pattern.Length; i++)
            {
                if (pattern[i] == '*')
                {
                    starIdx = i;
                    break;
                }
            }

            if (starIdx == -1)
            {
                version = 0;
                return false;
            }

            for (int i = 0; i < data.Length - 24; i++)
            {
                bool ok = true;
                for (int k = 0; k < starIdx; k++)
                {
                    if (data[i + k] != pattern[k])
                    {
                        ok = false;
                        break;
                    }
                }

                if (!ok) continue;

                int j = i + starIdx;
                version = 0;
                int digits = 0;
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

                for (int k = starIdx + 1; k < pattern.Length; k++)
                {
                    if (data[j + k - starIdx - 1] != pattern[k])
                    {
                        ok = false;
                        break;
                    }
                }

                if (!ok) continue;

                return true;
            }

            version = 0;
            return false;
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
