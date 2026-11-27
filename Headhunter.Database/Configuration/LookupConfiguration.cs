using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Headhunter.Database.Configuration;

// The names are searched by prefix (the district suggestions), so each has an index.
internal class CountyConfiguration : IEntityTypeConfiguration<County>
{
    public void Configure(EntityTypeBuilder<County> builder)
    {
        builder.ToTable("Counties").HasKey(x => x.Code);
        builder.Property(x => x.Code).HasMaxLength(2).IsUnicode(false).ValueGeneratedNever();
        builder.Property(x => x.Name).HasMaxLength(100);
        builder.HasIndex(x => x.Name);
    }
}

internal class JurisdictionConfiguration : IEntityTypeConfiguration<Jurisdiction>
{
    // A few codes are used by more than one county, so a jurisdiction is its county and its code.
    public void Configure(EntityTypeBuilder<Jurisdiction> builder)
    {
        builder.ToTable("Jurisdictions").HasKey(x => new { x.CountyCode, x.Code });
        builder.Property(x => x.CountyCode).HasMaxLength(2).IsUnicode(false);
        builder.Property(x => x.Code).HasMaxLength(5).IsUnicode(false);
        builder.Property(x => x.Name).HasMaxLength(100);
        builder.HasIndex(x => x.Name);
        builder.HasOne(x => x.County).WithMany().HasForeignKey(x => x.CountyCode).OnDelete(DeleteBehavior.Restrict);
    }
}

internal class SchoolDistrictConfiguration : IEntityTypeConfiguration<SchoolDistrict>
{
    public void Configure(EntityTypeBuilder<SchoolDistrict> builder)
    {
        builder.ToTable("SchoolDistricts").HasKey(x => x.Code);
        builder.Property(x => x.Code).HasMaxLength(5).IsUnicode(false).ValueGeneratedNever();
        builder.Property(x => x.Name).HasMaxLength(100);
        builder.HasIndex(x => x.Name);
    }
}

internal class StateHouseDistrictConfiguration : IEntityTypeConfiguration<StateHouseDistrict>
{
    public void Configure(EntityTypeBuilder<StateHouseDistrict> builder)
    {
        builder.ToTable("StateHouseDistricts").HasKey(x => x.Code);
        builder.Property(x => x.Code).HasMaxLength(5).IsUnicode(false).ValueGeneratedNever();
        builder.Property(x => x.Name).HasMaxLength(100);
        builder.HasIndex(x => x.Name);
    }
}

internal class StateSenateDistrictConfiguration : IEntityTypeConfiguration<StateSenateDistrict>
{
    public void Configure(EntityTypeBuilder<StateSenateDistrict> builder)
    {
        builder.ToTable("StateSenateDistricts").HasKey(x => x.Code);
        builder.Property(x => x.Code).HasMaxLength(5).IsUnicode(false).ValueGeneratedNever();
        builder.Property(x => x.Name).HasMaxLength(100);
        builder.HasIndex(x => x.Name);
    }
}

internal class UsCongressDistrictConfiguration : IEntityTypeConfiguration<UsCongressDistrict>
{
    public void Configure(EntityTypeBuilder<UsCongressDistrict> builder)
    {
        builder.ToTable("UsCongressDistricts").HasKey(x => x.Code);
        builder.Property(x => x.Code).HasMaxLength(5).IsUnicode(false).ValueGeneratedNever();
        builder.Property(x => x.Name).HasMaxLength(100);
        builder.HasIndex(x => x.Name);
    }
}

internal class CountyCommissionerDistrictConfiguration : IEntityTypeConfiguration<CountyCommissionerDistrict>
{
    public void Configure(EntityTypeBuilder<CountyCommissionerDistrict> builder)
    {
        builder.ToTable("CountyCommissionerDistricts").HasKey(x => new { x.CountyCode, x.Code });
        builder.Property(x => x.CountyCode).HasMaxLength(2).IsUnicode(false);
        builder.Property(x => x.Code).HasMaxLength(5).IsUnicode(false);
        builder.Property(x => x.Name).HasMaxLength(100);
        builder.HasIndex(x => x.Name);
        builder.HasOne(x => x.County).WithMany().HasForeignKey(x => x.CountyCode).OnDelete(DeleteBehavior.Restrict);
    }
}

internal class ElectionConfiguration : IEntityTypeConfiguration<Election>
{
    public void Configure(EntityTypeBuilder<Election> builder)
    {
        builder.ToTable("Elections").HasKey(x => x.Date);
        builder.Property(x => x.Date).ValueGeneratedNever();
        builder.Property(x => x.Description).HasMaxLength(100);
    }
}

internal class VoteHistoryConfiguration : IEntityTypeConfiguration<VoteHistory>
{
    public void Configure(EntityTypeBuilder<VoteHistory> builder)
    {
        // Clustered on the voter: a voter's history is read together. About 62M rows for Michigan back to 2004.
        builder.ToTable("VoteHistory").HasKey(x => new { x.VoterID, x.ElectionDate });

        builder.HasOne(x => x.Voter).WithMany().HasForeignKey(x => x.VoterID).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Election).WithMany().HasForeignKey(x => x.ElectionDate).OnDelete(DeleteBehavior.Restrict);
    }
}
