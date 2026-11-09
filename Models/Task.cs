using System;
using System.Collections.Generic;

namespace Trpo8.Models;

public partial class Task
{
    public int Id { get; set; }

    public string Title { get; set; } = null!;

    public string Description { get; set; } = null!;

    public string OwnerLogin { get; set; } = null!;

    public bool IsCompeted { get; set; }
}
