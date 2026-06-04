using FurniSpace.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurniSpace.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260604154026_AlignProductSchemaWithEntities")]
    partial class AlignProductSchemaWithEntities
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
#pragma warning restore 612, 618
        }
    }
}
