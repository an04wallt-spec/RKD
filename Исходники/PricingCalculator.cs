using System;
using System.Collections.Generic;
using System.Linq;

namespace RkdEstimator
{
    public static class PricingCalculator
    {
        public static decimal GetSizeCoefficient(decimal widthMm, decimal heightMm, decimal depthMm)
        {
            // Простая и проверяемая модель: сумма трёх габаритов относительно 3000 мм.
            // Малые изделия не опускаются ниже 0,80; особо крупные ограничены 2,50.
            decimal coefficient = (widthMm + heightMm + depthMm) / 3000m;
            return Math.Max(0.80m, Math.Min(2.50m, coefficient));
        }

        public static PriceResult Calculate(decimal widthMm, decimal heightMm, decimal depthMm,
            decimal baseRate, decimal minimumPrice, decimal roundTo, IEnumerable<decimal> selectedPercents, decimal surchargePercent = 0m, decimal measurementFee = 0m)
        {
            decimal sizeCoefficient = GetSizeCoefficient(widthMm, heightMm, depthMm);
            decimal complexityPercent = selectedPercents == null ? 0m : selectedPercents.Sum();
            decimal sizeAdjustedBase = baseRate * sizeCoefficient;
            decimal complexityAmount = sizeAdjustedBase * complexityPercent / 100m;
            decimal calculatedBeforeSurcharge = sizeAdjustedBase + complexityAmount;
            decimal recommendedBeforeSurcharge = Math.Max(minimumPrice, calculatedBeforeSurcharge);
            decimal surchargeAmount = recommendedBeforeSurcharge * Math.Max(0m, surchargePercent) / 100m;
            decimal workCalculated = calculatedBeforeSurcharge * (1m + Math.Max(0m, surchargePercent) / 100m);
            decimal workRecommended = RoundUp(recommendedBeforeSurcharge + surchargeAmount, roundTo);
            decimal measurementAmount = Math.Max(0m, measurementFee);
            decimal calculated = workCalculated + measurementAmount;
            decimal recommended = workRecommended + measurementAmount;

            return new PriceResult
            {
                SizeCoefficient = sizeCoefficient,
                ComplexityPercent = complexityPercent,
                SizeAdjustedBase = sizeAdjustedBase,
                ComplexityAmount = complexityAmount,
                SurchargePercent = Math.Max(0m, surchargePercent),
                SurchargeAmount = surchargeAmount,
                WorkCalculatedPrice = workCalculated,
                WorkRecommendedPrice = workRecommended,
                MeasurementAmount = measurementAmount,
                CalculatedPrice = calculated,
                RecommendedPrice = recommended,
                MinimumApplied = minimumPrice > calculated
            };
        }

        public static decimal RoundUp(decimal value, decimal step)
        {
            if (step <= 0m) return Math.Round(value, 0, MidpointRounding.AwayFromZero);
            return Math.Ceiling(value / step) * step;
        }
    }
}
