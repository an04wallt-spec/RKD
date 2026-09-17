using System;
using System.Collections.Generic;

namespace RkdEstimator
{
    [Serializable]
    public sealed class ComplexityOption
    {
        public string Id { get; set; }
        public string Group { get; set; }
        public string Name { get; set; }
        public decimal Percent { get; set; }

        public ComplexityOption() { }

        public ComplexityOption(string id, string group, string name, decimal percent)
        {
            Id = id;
            Group = group;
            Name = name;
            Percent = percent;
        }
    }

    [Serializable]
    public sealed class AppSettings
    {
        public decimal BaseRate { get; set; }
        public decimal MinimumPrice { get; set; }
        public decimal MeasurementFee { get; set; }
        public decimal RoundTo { get; set; }
        public List<ComplexityOption> Options { get; set; }

        public static AppSettings CreateDefault()
        {
            return new AppSettings
            {
                BaseRate = 5000m,
                MinimumPrice = 7000m,
                MeasurementFee = 5000m,
                RoundTo = 500m,
                Options = new List<ComplexityOption>
                {
                    new ComplexityOption("payment_cashless", "Оплата", "Оплата по безналу", 7m),
                    new ComplexityOption("greed_tax", "Оплата", "Налог на жадность (для скидки)", 10m),
                    new ComplexityOption("kdz", "Оплата", "Создание КДЗ (для согласования Заказчиком)", 20m),

                    new ComplexityOption("veneer", "Материалы и отделка", "Шпон", 5m),
                    new ComplexityOption("solid_wood", "Материалы и отделка", "Массив дерева", 10m),
                    new ComplexityOption("glass", "Материалы и отделка", "Стекло / зеркало / витрины", 5m),
                    new ComplexityOption("metal", "Материалы и отделка", "Металлические элементы", 10m),
                    new ComplexityOption("stone", "Материалы и отделка", "Камень / керамика", 5m),
                    new ComplexityOption("mixed", "Материалы и отделка", "Много разных материалов / цветов", 6m),
                    new ComplexityOption("nonstandard_panels", "Материалы и отделка", "Нестандартные толщины панелей", 5m),
                    new ComplexityOption("soft_materials", "Материалы и отделка", "Ткань / кожа / мягкие элементы", 6m),

                    new ComplexityOption("doors", "Конструкция", "Много дверей / фасадов", 8m),
                    new ComplexityOption("drawers", "Конструкция", "Много ящиков и выдвижных элементов", 7m),
                    new ComplexityOption("curves", "Конструкция", "Радиусные детали", 15m),
                    new ComplexityOption("bent", "Конструкция", "Гнутые элементы", 15m),
                    new ComplexityOption("complex_shapes", "Конструкция", "Лекальные детали / сложная форма", 10m),
                    new ComplexityOption("geometry", "Конструкция", "Нестандартная геометрия / углы", 15m),
                    new ComplexityOption("asymmetry", "Конструкция", "Асимметрия и разные секции", 8m),
                    new ComplexityOption("milled", "Конструкция", "Фрезерованные фасады / детали", 5m),
                    new ComplexityOption("profiles", "Конструкция", "Сложные профили / раскладки", 10m),
                    new ComplexityOption("unique", "Конструкция", "Много уникальных деталей", 15m),

                    new ComplexityOption("lighting", "Оснащение", "Подсветка / электрика", 7m),
                    new ComplexityOption("appliances", "Оснащение", "Встроенная техника", 6m),
                    new ComplexityOption("hardware", "Оснащение", "Нестандартная фурнитура", 8m),
                    new ComplexityOption("hardware_installation", "Оснащение", "Установка фурнитуры", 20m),
                    new ComplexityOption("mechanisms", "Оснащение", "Нестандартные механизмы", 10m),
                    new ComplexityOption("transform", "Оснащение", "Трансформация / подвижные узлы", 20m),
                    new ComplexityOption("hidden_fasteners", "Оснащение", "Скрытые крепления", 5m),

                    new ComplexityOption("builtin", "Привязки и монтаж", "Встроенное изделие / ниша", 12m),
                    new ComplexityOption("tight", "Привязки и монтаж", "Жёсткая привязка к помещению", 10m),
                    new ComplexityOption("joints", "Привязки и монтаж", "Сложные примыкания", 10m),
                    new ComplexityOption("utilities", "Привязки и монтаж", "Обход коммуникаций / техники", 10m),
                    new ComplexityOption("linked_items", "Привязки и монтаж", "Сопряжение нескольких изделий", 10m),
                    new ComplexityOption("site_measure", "Привязки и монтаж", "Выезд специалиста на объект и замер", 0m),

                    new ComplexityOption("only_visual", "Исходные данные и разработка", "Есть только визуализация", 10m),
                    new ComplexityOption("no_dimensions", "Исходные данные и разработка", "Нет точных размеров", 8m),
                    new ComplexityOption("hardware_unknown", "Исходные данные и разработка", "Не определена фурнитура", 7m),
                    new ComplexityOption("materials_unknown", "Исходные данные и разработка", "Не определены материалы / толщины", 5m),
                    new ComplexityOption("mechanisms_unknown", "Исходные данные и разработка", "Не определены механизмы", 7m),
                    new ComplexityOption("solution_search", "Исходные данные и разработка", "Требуется поиск технического решения", 15m),
                    new ComplexityOption("custom_node", "Исходные данные и разработка", "Разработка нестандартного узла", 15m),
                    new ComplexityOption("component_selection", "Исходные данные и разработка", "Подбор комплектующих", 8m),
                    new ComplexityOption("coordination", "Исходные данные и разработка", "Согласование со смежниками", 8m),

                    new ComplexityOption("detail", "Документация и условия", "Повышенная детализация чертежей", 10m),
                    new ComplexityOption("variants", "Документация и условия", "Несколько вариантов исполнения", 10m),
                    new ComplexityOption("urgent", "Документация и условия", "Срочная разработка", 20m),
                    new ComplexityOption("changes", "Документация и условия", "Изменения после утверждения", 15m),
                    new ComplexityOption("foreign_docs", "Документация и условия", "Переработка чужой документации", 10m),
                    new ComplexityOption("production_files", "Документация и условия", "Специальные файлы для производства", 8m),
                    new ComplexityOption("supervision", "Документация и условия", "Технический / авторский надзор", 10m)
                }
            };
        }
    }

    public sealed class PriceResult
    {
        public decimal SizeCoefficient { get; set; }
        public decimal ComplexityPercent { get; set; }
        public decimal SizeAdjustedBase { get; set; }
        public decimal ComplexityAmount { get; set; }
        public decimal SurchargePercent { get; set; }
        public decimal SurchargeAmount { get; set; }
        public decimal WorkCalculatedPrice { get; set; }
        public decimal WorkRecommendedPrice { get; set; }
        public decimal MeasurementAmount { get; set; }
        public decimal CalculatedPrice { get; set; }
        public decimal RecommendedPrice { get; set; }
        public bool MinimumApplied { get; set; }
    }
}
