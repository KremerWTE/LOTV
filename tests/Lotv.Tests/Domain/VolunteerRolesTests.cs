using Lotv.Core.Models;

namespace Lotv.Tests.Domain;

/// <summary>A volunteer can hold more than one role, e.g. Package Assembler and Prayer Ambassador.</summary>
public class VolunteerRolesTests
{
    [Fact]
    public void APrimaryRoleAlone_HasJustThatOneRole()
    {
        var v = new Volunteer { Role = VolunteerRole.PackageAssembler };
        Assert.Equal([VolunteerRole.PackageAssembler], v.Roles);
        Assert.True(v.HasRole(VolunteerRole.PackageAssembler));
        Assert.False(v.HasRole(VolunteerRole.PrayerAmbassador));
    }

    [Fact]
    public void AnAdditionalRole_IsHeldAlongsideThePrimaryOne()
    {
        var v = new Volunteer { Role = VolunteerRole.PackageAssembler, AdditionalRoles = "PrayerAmbassador" };
        Assert.True(v.HasRole(VolunteerRole.PackageAssembler));
        Assert.True(v.HasRole(VolunteerRole.PrayerAmbassador));
        Assert.False(v.HasRole(VolunteerRole.Driver));
        Assert.Equal(2, v.Roles.Count);
    }

    [Fact]
    public void JunkOrEmptyAdditionalRoles_AreIgnoredRatherThanThrowing()
    {
        var v = new Volunteer { Role = VolunteerRole.Driver, AdditionalRoles = "NotARealRole, , PrayerAmbassador" };
        Assert.True(v.HasRole(VolunteerRole.PrayerAmbassador));
        Assert.Single(Volunteer.ParseAdditionalRoles(v.AdditionalRoles));

        var none = new Volunteer { Role = VolunteerRole.Driver, AdditionalRoles = null };
        Assert.Single(none.Roles);
    }

    [Fact]
    public void ToCsv_RoundTripsThroughParse_WithoutDuplicates()
    {
        var csv = Volunteer.ToCsv([VolunteerRole.PrayerAmbassador, VolunteerRole.EventHelper, VolunteerRole.PrayerAmbassador]);
        Assert.Equal(new[] { VolunteerRole.PrayerAmbassador, VolunteerRole.EventHelper }, Volunteer.ParseAdditionalRoles(csv));
    }
}
