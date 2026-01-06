using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VeloStore.ViewModels;

namespace VeloStore.Models
{
    public class Cart
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; }

        public ApplicationUser User { get; set; }

        public ICollection<CartItem> Items { get; set; } = new List<CartItem>();
    }
}
