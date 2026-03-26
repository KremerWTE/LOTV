using Lotv.Core.ValueObjects;

namespace Lotv.Tests.ValueObjects;

public class ContactInfoTests
{
    [Fact]
    public void DefaultPreferredContact_IsEmail()
    {
        var c = new ContactInfo("555-1234", "a@b.com");
        Assert.Equal(PreferredContact.Email, c.PreferredContact);
    }

    [Fact]
    public void StoresPhoneAndEmail()
    {
        var c = new ContactInfo("555-9999", "test@example.com");
        Assert.Equal("555-9999", c.Phone);
        Assert.Equal("test@example.com", c.Email);
    }

    [Fact]
    public void NullPhone_IsAllowed()
    {
        var c = new ContactInfo(null, "only@email.com");
        Assert.Null(c.Phone);
        Assert.Equal("only@email.com", c.Email);
    }

    [Fact]
    public void NullEmail_IsAllowed()
    {
        var c = new ContactInfo("555-0000", null, PreferredContact.Phone);
        Assert.Null(c.Email);
        Assert.Equal(PreferredContact.Phone, c.PreferredContact);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = new ContactInfo("555-1111", "eq@test.com", PreferredContact.Text);
        var b = new ContactInfo("555-1111", "eq@test.com", PreferredContact.Text);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Inequality_DifferentPreference()
    {
        var a = new ContactInfo("555-2222", "neq@test.com", PreferredContact.Email);
        var b = new ContactInfo("555-2222", "neq@test.com", PreferredContact.Phone);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void AllThreePreferredContactValues_AreStoredCorrectly()
    {
        Assert.Equal(PreferredContact.Email, new ContactInfo(null, null, PreferredContact.Email).PreferredContact);
        Assert.Equal(PreferredContact.Phone, new ContactInfo(null, null, PreferredContact.Phone).PreferredContact);
        Assert.Equal(PreferredContact.Text,  new ContactInfo(null, null, PreferredContact.Text).PreferredContact);
    }
}
