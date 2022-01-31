using Alturos.Yolo.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Alturos.Yolo
{
    public class YoloWrapper : IDisposable
    {
        public const int MaxObjects = 1000;
        private const string YoloLibraryCpu = "yolo_cpp_dll_cpu";
        private const string YoloLibraryGpu = "yolo_cpp_dll_gpu";

        private readonly ImageAnalyzer _imageAnalyzer = new ImageAnalyzer();
        private readonly IYoloSystemValidator _yoloSystemValidator;
        private YoloObjectTypeResolver _objectTypeResolver;

        public DetectionSystem DetectionSystem { get; private set; } = DetectionSystem.Unknown;

        #region DllImport Cpu

        [DllImport(YoloLibraryCpu, EntryPoint = "init")]
        private static extern int InitializeYoloCpu(string configurationFilename, string weightsFilename, int gpuIndex);

        [DllImport(YoloLibraryCpu, EntryPoint = "detect_image")]
        private static extern int DetectImageCpu(string filename, ref BboxContainer container);

        [DllImport(YoloLibraryCpu, EntryPoint = "detect_mat")]
        private static extern int DetectImageCpu(IntPtr pArray, int nSize, ref BboxContainer container);

        [DllImport(YoloLibraryCpu, EntryPoint = "dispose")]
        private static extern int DisposeYoloCpu();

        [DllImport(YoloLibraryCpu, EntryPoint = "built_with_opencv")]
        private static extern bool BuiltWithOpenCV();

        #endregion

        #region DllImport Gpu

        [DllImport(YoloLibraryGpu, EntryPoint = "init")]
        private static extern int InitializeYoloGpuWithBatchSize(string configurationFilename, string weightsFilename, int gpuIndex, int batchSize);
      
        [DllImport(YoloLibraryGpu, EntryPoint = "init")]
        private static extern int InitializeYoloGpu(string configurationFilename, string weightsFilename, int gpuIndex);

        [DllImport(YoloLibraryGpu, EntryPoint = "detect_image")]
        private static extern int DetectImageGpu(string filename, ref BboxContainer container);

        [DllImport(YoloLibraryGpu, EntryPoint = "detect_mat")]
        private static extern int DetectImageGpu(IntPtr pArray, int nSize, ref BboxContainer container);

        [DllImport(YoloLibraryGpu, EntryPoint = "dispose")]
        private static extern int DisposeYoloGpu();

        [DllImport(YoloLibraryGpu, EntryPoint = "get_device_count")]
        private static extern int GetDeviceCount();

        [DllImport(YoloLibraryGpu, EntryPoint = "get_device_name")]
        private static extern int GetDeviceName(int gpu, StringBuilder deviceName);

#endregion

        /// <summary>
        /// Initialize Yolo
        /// </summary>
        /// <param name="yoloConfiguration"></param>
        /// <param name="gpuConfig">GPU Configuration</param>
        /// <param name="yoloSystemValidator">Yolo System validator</param>
        /// <exception cref="NotSupportedException">Thrown when the process not run in 64bit</exception>
        /// <exception cref="YoloInitializeException">Thrown if an error occurs during initialization</exception>
        public YoloWrapper(YoloConfiguration yoloConfiguration, GpuConfig gpuConfig = null, IYoloSystemValidator yoloSystemValidator = null)
        {
            if (yoloSystemValidator == null)
            {
                yoloSystemValidator = new DefaultYoloSystemValidator();
            }

            _yoloSystemValidator = yoloSystemValidator;
            Initialize(yoloConfiguration.ConfigFile, yoloConfiguration.WeightsFile, yoloConfiguration.NamesFile, gpuConfig);
        }

        /// <summary>
        /// Initialize Yolo
        /// </summary>
        /// <param name="configurationFilename">Yolo configuration (.cfg) file path</param>
        /// <param name="weightsFilename">Yolo trained data (.weights) file path</param>
        /// <param name="namesFilename">Yolo object names (.names) file path</param>
        /// <param name="gpuConfig">Gpu Index if multiple graphic devices available</param>
        /// <param name="yoloSystemValidator">Yolo System validator</param>
        /// <exception cref="NotSupportedException">Thrown when the process not run in 64bit</exception>
        /// <exception cref="YoloInitializeException">Thrown if an error occurs during initialization</exception>
        public YoloWrapper(string configurationFilename, string weightsFilename, string namesFilename, GpuConfig gpuConfig = null, IYoloSystemValidator yoloSystemValidator = null)
        {
            if (yoloSystemValidator == null)
            {
                yoloSystemValidator = new DefaultYoloSystemValidator();
            }

            _yoloSystemValidator = yoloSystemValidator;
            Initialize(configurationFilename, weightsFilename, namesFilename, gpuConfig);
        }

        public void Dispose()
        {
            switch (DetectionSystem)
            {
                case DetectionSystem.CPU:
                    DisposeYoloCpu();
                    break;
                case DetectionSystem.GPU:

                    DisposeYoloGpu();
                    break;
            }
        }

        private void Initialize(string configurationFilename, string weightsFilename, string namesFilename, GpuConfig gpuConfig, int batchSize = 1)
        {
            if (IntPtr.Size != 8)
            {
                throw new NotSupportedException("Only 64-bit processes are supported");
            }

            var systemReport = _yoloSystemValidator.Validate();
            if (!systemReport.MicrosoftVisualCPlusPlusRedistributableExists)
            {
                throw new YoloInitializeException("Microsoft Visual C++ 2017-2019 Redistributable (x64)");
            }

            DetectionSystem = DetectionSystem.CPU;

            int gpuIndex = 0;
            if (gpuConfig != null)
            {
                if (!systemReport.YoloGpuDll)
                {
                    throw new YoloInitializeException("yolo_cpp_dll_gpu.dll not found");
                }

                if (!systemReport.CudaExists)
                {
                    throw new YoloInitializeException("CUDA files not found");
                }

                if (!systemReport.CudnnExists)
                {
                    throw new YoloInitializeException("cuDNN not found");
                }

                var deviceCount = GetDeviceCount();
                if (deviceCount == 0)
                {
                    throw new YoloInitializeException("No NVIDIA graphic device is available");
                }

                if (gpuConfig.GpuIndex >= deviceCount)
                {
                    throw new YoloInitializeException("Graphic device index is out of range");
                }

                DetectionSystem = DetectionSystem.GPU;
                gpuIndex = gpuConfig.GpuIndex;
            }

            switch (DetectionSystem)
            {
                case DetectionSystem.CPU:
                    InitializeYoloCpu(configurationFilename, weightsFilename, 0);
                    break;
                case DetectionSystem.GPU:

                    if (_yoloSystemValidator.IsCudaVersion110())
                    {
                        InitializeYoloGpuWithBatchSize(configurationFilename, weightsFilename, gpuIndex, batchSize);
                    }
                    else
                    {
                        InitializeYoloGpu(configurationFilename, weightsFilename, gpuIndex);
                    }

               
                    break;
            }

            _objectTypeResolver = new YoloObjectTypeResolver(namesFilename);
        }

        /// <summary>
        /// Detect objects on an image
        /// </summary>
        /// <param name="filepath"></param>
        /// <returns></returns>
        /// <exception cref="FileNotFoundException">Thrown when the filepath is wrong</exception>
        public IEnumerable<YoloItem> Detect(string filepath)
        {
            if (!File.Exists(filepath))
            {
                throw new FileNotFoundException("Cannot find the file", filepath);
            }

            var container = new BboxContainer();
            var count = 0;
            switch (DetectionSystem)
            {
                case DetectionSystem.CPU:
                    count = DetectImageCpu(filepath, ref container);
                    break;
                case DetectionSystem.GPU:
                    count = DetectImageGpu(filepath, ref container);
                    break;
            }

            if (count == -1)
            {
                throw new NotImplementedException("C++ dll compiled incorrectly");
            }

            return Convert(container);
        }

        /// <summary>
        /// Detect objects on an image
        /// </summary>
        /// <param name="imageData"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException">Thrown when the yolo_cpp dll is wrong compiled</exception>
        /// <exception cref="Exception">Thrown when the byte array is not a valid image</exception>
        public unsafe IEnumerable<YoloItem> Detect(byte[] imageData)
        {
            if (!_imageAnalyzer.IsValidImageFormat(imageData))
            {
                throw new Exception("Invalid image data, wrong image format");
            }

            var container = new BboxContainer();
            var count = 0;
            try
            {
                fixed (byte* pnt = imageData)
                {
                    switch (DetectionSystem)
                    {
                        case DetectionSystem.CPU:
                            count = DetectImageCpu((IntPtr)pnt, imageData.Length, ref container);
                            break;
                        case DetectionSystem.GPU:
                            count = DetectImageGpu((IntPtr)pnt, imageData.Length, ref container);
                            break;
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }

            if (count == -1)
            {
                throw new NotImplementedException("C++ dll compiled incorrectly");
            }

            return Convert(container);
        }

        /// <summary>
        /// Detect objects on an image
        /// </summary>
        /// <param name="imagePtr"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException">Thrown when the yolo_cpp dll is wrong compiled</exception>
        public IEnumerable<YoloItem> Detect(IntPtr imagePtr, int size)
        {
            var container = new BboxContainer();

            var count = 0;
            try
            {
                switch (DetectionSystem)
                {
                    case DetectionSystem.CPU:
                        count = DetectImageCpu(imagePtr, size, ref container);
                        break;
                    case DetectionSystem.GPU:
                        count = DetectImageGpu(imagePtr, size, ref container);
                        break;
                }
            }
            catch (Exception)
            {
                return null;
            }

            if (count == -1)
            {
                throw new NotImplementedException("C++ dll compiled incorrectly");
            }

            return Convert(container);
        }

        public string GetGraphicDeviceName(GpuConfig gpuConfig)
        {
            if (gpuConfig == null)
            {
                return string.Empty;
            }

            var systemReport = _yoloSystemValidator.Validate();
            if (!systemReport.CudaExists || !systemReport.CudnnExists)
            {
                return "unknown";
            }

            var deviceName = new StringBuilder(); //allocate memory for string
            GetDeviceName(gpuConfig.GpuIndex, deviceName);
            return deviceName.ToString();
        }

        public bool IsBuiltWithOpenCV()
        {
            return BuiltWithOpenCV();
        }

        private IEnumerable<YoloItem> Convert(BboxContainer container)
        {
            return container.candidates.Where(o => o.h > 0 || o.w > 0).Select(o =>

                new YoloItem
                {
                    X = (int)o.x,
                    Y = (int)o.y,
                    Height = (int)o.h,
                    Width = (int)o.w,
                    Confidence = o.prob,
                    Type = _objectTypeResolver.Resolve((int)o.obj_id)
                }
            );
        }
    }
}
