using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VeloStore.Models
{
    public class CartItem
    {
        [Key]
        public int Id { get; set; }

        public int CartId { get; set; }
        public Cart Cart { get; set; }

        public int ProductId { get; set; }

        public string ProductName { get; set; }
        public decimal Price { get; set; }
        public string ImageUrl { get; set; }

        public int Quantity { get; set; }
    }
}
