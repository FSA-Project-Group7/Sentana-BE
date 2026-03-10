namespace Sentana.API.DTOs.Utility
{
    public class UtilityHistoryDto
    {
        public int Month { get; set; }
        public int Year { get; set; }

        // thông số điện
        public decimal ElectricityOldIndex { get; set; }
        public decimal ElectricityNewIndex { get; set; }
        public decimal ElectricityConsumption => ElectricityNewIndex - ElectricityOldIndex; // Tự động tính

        // thông số nước
        public decimal WaterOldIndex { get; set; }
        public decimal WaterNewIndex { get; set; }
        public decimal WaterConsumption => WaterNewIndex - WaterOldIndex; // Tự động tính
    }
}