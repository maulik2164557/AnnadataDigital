using System;
using System.Collections.Generic;

namespace AgriDirect.Models.ViewModels
{
    public class AdminDashboardViewModel
    {
        public int TotalUsers { get; set; }
        public int TotalFarmers { get; set; }
        public int TotalConsumers { get; set; }
        public int TotalProducts { get; set; }
        public int PendingUserApprovals { get; set; }

        public List<User> RecentUsers { get; set; } = new List<User>();
        public List<Product> RecentProducts { get; set; } = new List<Product>();
        public List<User> PendingFarmers { get; set; } = new List<User>();
    }

    public class ReportsViewModel
    {
        public decimal TotalSalesRevenue { get; set; }
        public int CompletedOrdersCount { get; set; }
        public int PendingOrdersCount { get; set; }
        public int CancelledOrdersCount { get; set; }

        public Dictionary<string, int> ProductsByCategory { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, decimal> SalesByCategory { get; set; } = new Dictionary<string, decimal>();
        public List<FarmerPerformanceReportItem> TopFarmers { get; set; } = new List<FarmerPerformanceReportItem>();
    }

    public class FarmerPerformanceReportItem
    {
        public string FarmerName { get; set; } = string.Empty;
        public string FarmName { get; set; } = string.Empty;
        public int TotalOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public double AverageRating { get; set; }
    }
}
