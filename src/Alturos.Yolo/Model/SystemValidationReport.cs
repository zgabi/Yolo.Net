namespace Alturos.Yolo.Model
{
    public class SystemValidationReport
    {
        //Microsoft Visual C++ 2017/2019 Redistributable
        public bool MicrosoftVisualCPlusPlusRedistributableExists { get; set; }

        //NVIDIA CUDA Toolkit 10.1+
        public bool CudaExists { get; set; }
        
        //NVIDIA cuDNN v7.6.5 for CUDA 10.1+ (CUDA version must match)
        public bool CudnnExists { get; set; }
    }
}
