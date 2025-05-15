using Makeup.Models;
using System.Collections.Generic;

namespace Makeup.ViewModels
{
    public class ServiceListVM
    {
        public IEnumerable<Service> Services { get; set; } = new List<Service>();
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public byte? Status { get; set; }
        public int TotalItems { get; set; }
        public int PageSize { get; set; }

        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;
    }
} 