using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace OctoDI.Web.Models.DatabaseModels;

public partial class User
{
    public int UserId { get; set; }

    [Required]
    public string Username { get; set; } = null!;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = null!;

    [Required]
    [DataType(DataType.Password)]
    public string PasswordHash { get; set; } = null!;

    public string? PasswordSalt { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }

    public string UserRole { get; set; } = null!;
    public int? SubscriptionId { get; set; }
    public bool IsActive { get; set; }
    public bool IsTwoFactorEnabled { get; set; } = false;

    public DateTime? LastLoginDate { get; set; }
    public string? ProfileImageUrl { get; set; }
    public DateTime CreatedDate { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public int? UpdatedBy { get; set; }
    public string? Remarks { get; set; }


    public virtual Subscription? Subscription { get; set; }

}
