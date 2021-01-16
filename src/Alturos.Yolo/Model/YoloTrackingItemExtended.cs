using System;

namespace Alturos.Yolo.Model
{
    internal class YoloTrackingItemExtended : YoloItem
    {
        public string ObjectId { get; }

        public int ProcessIndex { get; set; }
        
        public double TrackingConfidence { get; private set; }

        public YoloTrackingItemExtended(YoloItem item, string objectId)
        {
            ObjectId = objectId;

            Type = item.Type;
            Confidence = item.Confidence;
            X = item.X;
            Y = item.Y;
            Width = item.Width;
            Height = item.Height;
        }

        public void IncreaseTrackingConfidence()
        {
            if (TrackingConfidence < 100)
            {
                TrackingConfidence += 5;
            }
        }

        public void DecreaseTrackingConfidence()
        {
            if (TrackingConfidence > 0)
            {
                TrackingConfidence -= 5;
            }
        }
    }
}
