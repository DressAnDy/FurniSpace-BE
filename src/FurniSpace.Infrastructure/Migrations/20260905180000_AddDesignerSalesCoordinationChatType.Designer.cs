using FurniSpace.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurniSpace.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260905180000_AddDesignerSalesCoordinationChatType")]
public partial class AddDesignerSalesCoordinationChatType
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder.HasAnnotation("ProductVersion", "8.0.0");
#pragma warning restore 612, 618
    }
}
