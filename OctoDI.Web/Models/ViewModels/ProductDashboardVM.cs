using OctoDI.Web.Models.DatabaseModels;
using System.Collections.Generic;

namespace OctoDI.Web.Models.ViewModels
{
    public class ProductDashboardVM
    {
        public List<Unit>? Units { get; set; }
        public List<ServiceCategory>? Categories { get; set; }
        public List<Product>? Products { get; set; }
    }
}
