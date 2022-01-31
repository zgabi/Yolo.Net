using Alturos.Yolo.Model;

namespace Alturos.Yolo
{
    public interface IYoloSystemValidator
    {
        SystemValidationReport Validate();

        bool IsCudaVersion111();
    }
}
