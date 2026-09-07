using AutoMapper;
using ECommerce.Application.DTOs.Cart;
using ECommerce.Application.DTOs.Category;
using ECommerce.Application.DTOs.Order;
using ECommerce.Application.DTOs.Product;
using ECommerce.Domain.Entities.Cart;
using ECommerce.Domain.Entities.Catalog;
using ECommerce.Domain.Entities.Ordering;

namespace ECommerce.Application.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // Category Mappings
        CreateMap<Category, CategoryDto>();
        CreateMap<CategoryCreateDto, Category>();
        CreateMap<CategoryUpdateDto, Category>();

        // Product Mappings
        CreateMap<Product, ProductDto>()
            .ForCtorParam("CategoryName", opt => opt.MapFrom(src => src.Category != null ? src.Category.Name : string.Empty))
            .ForCtorParam("ImageUrls", opt => opt.MapFrom(src => src.Images.Select(i => i.ImageUrl).ToList()));

        CreateMap<ProductCreateDto, Product>();
        CreateMap<ProductUpdateDto, Product>();
        CreateMap<ProductImage, ProductImageDto>();

        // Cart Mappings
        CreateMap<CartItem, CartItemDto>()
            .ForCtorParam("ProductName", opt => opt.MapFrom(src => src.Product != null ? src.Product.Name : string.Empty))
            .ForCtorParam("ProductImageUrl", opt => opt.MapFrom(src => src.Product != null && src.Product.Images.Any() ? src.Product.Images.First().ImageUrl : null))
            .ForCtorParam("LiveUnitPrice", opt => opt.MapFrom(src => src.Product != null ? src.Product.Price : src.UnitPriceSnapshot))
            .ForCtorParam("PriceChanged", opt => opt.MapFrom(src => src.Product != null && src.UnitPriceSnapshot != src.Product.Price))
            .ForCtorParam("TotalPrice", opt => opt.MapFrom(src => src.Quantity * src.UnitPriceSnapshot));

        CreateMap<Cart, CartDto>()
            .ForCtorParam("Items", opt => opt.MapFrom(src => src.Items))
            .ForCtorParam("Subtotal", opt => opt.MapFrom(src => src.Items.Sum(i => i.Quantity * i.UnitPriceSnapshot)));

        // Order Mappings
        CreateMap<OrderItem, OrderItemDto>()
            .ForCtorParam("ProductName", opt => opt.MapFrom(src => src.ProductNameSnapshot))
            .ForCtorParam("TotalPrice", opt => opt.MapFrom(src => src.UnitPrice * src.Quantity));

        CreateMap<Order, OrderDto>()
            .ForCtorParam("Status", opt => opt.MapFrom(src => src.Status.ToString()))
            .ForCtorParam("CreatedAt", opt => opt.MapFrom(src => src.CreatedAtUtc))
            .ForCtorParam("Items", opt => opt.MapFrom(src => src.Items));
    }
}
