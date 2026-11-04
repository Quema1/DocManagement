using System;
using System.Collections.Generic;
using System.Text;

namespace DocManagement.Domain
{
	public class Document
	{
		public Guid Id { get; set; }
		public string? Name { get; set; }
		public string? Description { get; set; }
		public DateTime CreatedAt { get; set; }
		public DateTime UpdatedAt { get; set; }
		public string? PreviewUrl { get; set; }
		public PreviewStatus PreviewStatus { get; set; }
	}
}
