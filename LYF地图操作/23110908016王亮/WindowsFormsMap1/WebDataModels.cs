using System.Collections.Generic;

namespace WindowsFormsMap1
{
    // Models corresponding to VisualWeb/data/data.json Schema

    public class ProjectInfo
    {
        public string title { get; set; } = "山东省非物质文化遗产大数据概览";
        public int totalItems { get; set; }
        public string lastUpdated { get; set; }
    }

    public class CityStat
    {
        public string name { get; set; }
        public int value { get; set; }
    }

    public class CategoryStat
    {
        public string name { get; set; }
        public int count { get; set; }
    }

    public class ProjectPoint
    {
        public string name { get; set; }
        public string category { get; set; }
        public double x { get; set; }
        public double y { get; set; }
        public string city { get; set; }
    }

    public class WebData
    {
        public ProjectInfo projectInfo { get; set; }
        public List<CityStat> statsByCity { get; set; }
        public List<CategoryStat> categories { get; set; }
        public List<ProjectPoint> points { get; set; }

        public WebData()
        {
            projectInfo = new ProjectInfo();
            statsByCity = new List<CityStat>();
            categories = new List<CategoryStat>();
            points = new List<ProjectPoint>();
        }
    }
}
