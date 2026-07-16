using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using FINAPSA.Data;

#nullable disable

namespace FINAPSA.Migrations
{
    [DbContext(typeof(FINAPSADbContext))]
    [Migration("20260112120000_AddRrrNumberToPayment")]
    partial class AddRrrNumberToPayment
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
            // Migration designer placeholder. Model snapshot contains the canonical model.
        }
    }
}
