using System;
using System.Collections.Generic;
using System.Text;

namespace PROManagementSystem.Application.Common.Interfaces
{
    public  interface ISoftDeletable
    {
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
    }
}
