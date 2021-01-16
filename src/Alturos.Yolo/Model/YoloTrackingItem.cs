using System;

namespace Alturos.Yolo.Model
{
    public class YoloTrackingItem : YoloItem
    {
        public string ObjectId { get; }

        public YoloTrackingItem(YoloItem item, string objectId)
        {
            ObjectId = objectId;

            Type = item.Type;
            Confidence = item.Confidence;
            X = item.X;
            Y = item.Y;
            Width = item.Width;
            Height = item.Height;
        }
    }
}
