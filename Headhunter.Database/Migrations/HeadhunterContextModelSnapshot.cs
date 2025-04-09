﻿// <auto-generated />
using System;
using Headhunter.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace Headhunter.Database.Migrations
{
    [DbContext(typeof(HeadhunterContext))]
    partial class HeadhunterContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "9.0.3")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("Headhunter.Database.Address", b =>
                {
                    b.Property<Guid>("ID")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("CITY")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("COUNTY_CODE")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("COUNTY_COMMISSIONER_DISTRICT_CODE")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("COUNTY_COMMISSIONER_DISTRICT_NAME")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("COUNTY_NAME")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("DIRECTION_PREFIX")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("DIRECTION_SUFFIX")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("JURISDICTION_CODE")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("JURISDICTION_NAME")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<decimal?>("Latitude")
                        .HasPrecision(15, 12)
                        .HasColumnType("decimal(15,12)");

                    b.Property<decimal?>("Longitude")
                        .HasPrecision(15, 12)
                        .HasColumnType("decimal(15,12)");

                    b.Property<bool?>("Matched")
                        .HasColumnType("bit");

                    b.Property<string>("PRECINCT")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("SCHOOL_DISTRICT_CODE")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("SCHOOL_DISTRICT_NAME")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("SCHOOL_PRECINCT")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("STATE")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("STATE_HOUSE_DISTRICT_CODE")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("STATE_HOUSE_DISTRICT_NAME")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("STATE_SENATE_DISTRICT_CODE")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("STATE_SENATE_DISTRICT_NAME")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("STREET_NAME")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("STREET_NUMBER")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("STREET_NUMBER_PREFIX")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("STREET_NUMBER_SUFFIX")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("STREET_TYPE")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("US_CONGRESS_DISTRICT_CODE")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("US_CONGRESS_DISTRICT_NAME")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("VILLAGE_DISTRICT_CODE")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("VILLAGE_DISTRICT_NAME")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("VILLAGE_PRECINCT")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("WARD")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("ZIP_CODE")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("ID");

                    b.ToTable("Addresses", (string)null);
                });

            modelBuilder.Entity("Headhunter.Database.MichiganVoterRecord", b =>
                {
                    b.Property<Guid>("ID")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("CITY")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("COUNTY_CODE")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("COUNTY_COMMISSIONER_DISTRICT_CODE")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("COUNTY_COMMISSIONER_DISTRICT_NAME")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("COUNTY_NAME")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("DIRECTION_PREFIX")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("DIRECTION_SUFFIX")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("EXTENSION")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("FIRST_NAME")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("GENDER")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<bool>("IS_PERM_AV_APP_VOTER")
                        .HasColumnType("bit");

                    b.Property<bool>("IS_PERM_AV_BALLOT_VOTER")
                        .HasColumnType("bit");

                    b.Property<string>("JURISDICTION_CODE")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("JURISDICTION_NAME")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("LAST_NAME")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("MAILING_ADDRESS_LINE_FIVE")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("MAILING_ADDRESS_LINE_FOUR")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("MAILING_ADDRESS_LINE_ONE")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("MAILING_ADDRESS_LINE_THREE")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("MAILING_ADDRESS_LINE_TWO")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("MIDDLE_NAME")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("NAME_SUFFIX")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("PRECINCT")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime>("REGISTRATION_DATE")
                        .HasColumnType("datetime2");

                    b.Property<string>("SCHOOL_DISTRICT_CODE")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("SCHOOL_DISTRICT_NAME")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("SCHOOL_PRECINCT")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("STATE")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("STATE_HOUSE_DISTRICT_CODE")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("STATE_HOUSE_DISTRICT_NAME")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("STATE_SENATE_DISTRICT_CODE")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("STATE_SENATE_DISTRICT_NAME")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("STREET_NAME")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("STREET_NUMBER")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("STREET_NUMBER_PREFIX")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("STREET_NUMBER_SUFFIX")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("STREET_TYPE")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("UOCAVA_STATUS_CODE")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("UOCAVA_STATUS_NAME")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("US_CONGRESS_DISTRICT_CODE")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("US_CONGRESS_DISTRICT_NAME")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("VILLAGE_DISTRICT_CODE")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("VILLAGE_DISTRICT_NAME")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("VILLAGE_PRECINCT")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("VOTER_IDENTIFICATION_NUMBER")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("VOTER_STATUS_TYPE_CODE")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("WARD")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("YEAR_OF_BIRTH")
                        .HasColumnType("int");

                    b.Property<string>("ZIP_CODE")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("ID");

                    b.ToTable("MichiganVoterRecords", (string)null);
                });

            modelBuilder.Entity("Headhunter.Database.Voter", b =>
                {
                    b.Property<Guid>("ID")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("AddressID")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("EXTENSION")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("FIRST_NAME")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("GENDER")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<bool>("IS_PERM_AV_APP_VOTER")
                        .HasColumnType("bit");

                    b.Property<bool>("IS_PERM_AV_BALLOT_VOTER")
                        .HasColumnType("bit");

                    b.Property<string>("LAST_NAME")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("MAILING_ADDRESS_LINE_FIVE")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("MAILING_ADDRESS_LINE_FOUR")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("MAILING_ADDRESS_LINE_ONE")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("MAILING_ADDRESS_LINE_THREE")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("MAILING_ADDRESS_LINE_TWO")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("MIDDLE_NAME")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("NAME_SUFFIX")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime>("REGISTRATION_DATE")
                        .HasColumnType("datetime2");

                    b.Property<string>("UOCAVA_STATUS_CODE")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("UOCAVA_STATUS_NAME")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("VOTER_IDENTIFICATION_NUMBER")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("VOTER_STATUS_TYPE_CODE")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("YEAR_OF_BIRTH")
                        .HasColumnType("int");

                    b.HasKey("ID");

                    b.HasIndex("AddressID");

                    b.ToTable("Voters", (string)null);
                });

            modelBuilder.Entity("Headhunter.Database.Voter", b =>
                {
                    b.HasOne("Headhunter.Database.Address", "Address")
                        .WithMany("Voters")
                        .HasForeignKey("AddressID")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Headhunter.Database.MichiganVoterRecord", "MichiganVoterRecord")
                        .WithOne("Voter")
                        .HasForeignKey("Headhunter.Database.Voter", "ID");

                    b.Navigation("Address");

                    b.Navigation("MichiganVoterRecord");
                });

            modelBuilder.Entity("Headhunter.Database.Address", b =>
                {
                    b.Navigation("Voters");
                });

            modelBuilder.Entity("Headhunter.Database.MichiganVoterRecord", b =>
                {
                    b.Navigation("Voter");
                });
#pragma warning restore 612, 618
        }
    }
}
