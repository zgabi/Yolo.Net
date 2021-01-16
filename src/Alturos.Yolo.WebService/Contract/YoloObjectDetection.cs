using Alturos.Yolo.Model;
using System;
using System.Collections.Generic;

namespace Alturos.Yolo.WebService.Contract
{
    public class YoloObjectDetection : IObjectDetection, IDisposable
    {
        private YoloWrapper _yoloWrapper;

        public YoloObjectDetection()
        {
            var configurationDetector = new YoloConfigurationDetector();
            var configuration = configurationDetector.Detect();
            _yoloWrapper = new YoloWrapper(configuration);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            _yoloWrapper?.Dispose();
        }

        public IEnumerable<YoloItem> Detect(byte[] imageData)
        {
            return _yoloWrapper.Detect(imageData);
        }

        public IEnumerable<YoloItem> Detect(string filePath)
        {
            return _yoloWrapper.Detect(filePath);
        }
    }
}
