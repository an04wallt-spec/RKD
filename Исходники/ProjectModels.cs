using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace RkdEstimator
{
    [Serializable]
    public sealed class ProjectDocument
    {
        public string Name { get; set; }
        public List<ProjectRoom> Rooms { get; set; }
        public bool IncludeMeasurement { get; set; }
        public decimal MeasurementFee { get; set; }
        public bool CashlessPayment { get; set; }

        public static ProjectDocument CreateDefault(decimal measurementFee)
        {
            return new ProjectDocument
            {
                Name = "Новый проект",
                Rooms = new List<ProjectRoom> { new ProjectRoom { Name = "Помещение 1", Items = new List<ProjectItem>() } },
                MeasurementFee = measurementFee
            };
        }
    }

    [Serializable]
    public sealed class ProjectRoom
    {
        public string Name { get; set; }
        public List<ProjectItem> Items { get; set; }
    }

    [Serializable]
    public sealed class ProjectItem
    {
        public string Name { get; set; }
        public decimal Width { get; set; }
        public decimal Height { get; set; }
        public decimal Depth { get; set; }
        public decimal BaseRate { get; set; }
        public decimal MinimumPrice { get; set; }
        public decimal ClarifiedPrice { get; set; }
        public decimal FinalWorkPrice { get; set; }
        public List<string> CheckedOptionIds { get; set; }
        public List<string> ImagePaths { get; set; }

        public static ProjectItem CreateDefault(string name, AppSettings settings)
        {
            return new ProjectItem
            {
                Name = name,
                Width = 2000m,
                Height = 2200m,
                Depth = 600m,
                BaseRate = settings.BaseRate,
                MinimumPrice = settings.MinimumPrice,
                CheckedOptionIds = new List<string>(),
                ImagePaths = new List<string>()
            };
        }
    }

    public static class ProjectStore
    {
        public static void Save(string path, ProjectDocument project)
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
            using (FileStream stream = File.Create(path))
            {
#pragma warning disable 618
                new BinaryFormatter().Serialize(stream, project);
#pragma warning restore 618
            }
        }

        public static ProjectDocument Load(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            {
#pragma warning disable 618
                ProjectDocument project = new BinaryFormatter().Deserialize(stream) as ProjectDocument;
#pragma warning restore 618
                if (project == null) throw new InvalidDataException("Файл не содержит проект РКД.");
                if (project.Rooms == null) project.Rooms = new List<ProjectRoom>();
                foreach (ProjectRoom room in project.Rooms)
                {
                    if (room.Items == null) room.Items = new List<ProjectItem>();
                }
                return project;
            }
        }
    }
}
