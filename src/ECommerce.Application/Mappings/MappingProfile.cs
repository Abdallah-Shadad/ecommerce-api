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
            .ForCtorParam(nameof(CartItemDto.ProductId), opt => opt.MapFrom(src => src.ProductId))
            .ForCtorParam(nameof(CartItemDto.ProductName), opt => opt.MapFrom(src => src.Product != null ? src.Product.Name : string.Empty))
            .ForCtorParam(nameof(CartItemDto.ProductImageUrl), opt => opt.MapFrom(src => src.Product != null && src.Product.Images.Any() ? src.Product.Images.First().ImageUrl : null))
            .ForCtorParam(nameof(CartItemDto.CurrentPrice), opt => opt.MapFrom(src => src.Product != null ? src.Product.Price : src.UnitPriceSnapshot))
            .ForCtorParam(nameof(CartItemDto.UnitPriceSnapshot), opt => opt.MapFrom(src => src.UnitPriceSnapshot))
            .ForCtorParam(nameof(CartItemDto.HasPriceChanged), opt => opt.MapFrom(src => src.Product != null && src.UnitPriceSnapshot != src.Product.Price))
            .ForCtorParam(nameof(CartItemDto.Quantity), opt => opt.MapFrom(src => src.Quantity))
            .ForCtorParam(nameof(CartItemDto.TotalPrice), opt => opt.MapFrom(src => src.Quantity * src.UnitPriceSnapshot));

        CreateMap<Cart, CartDto>()
            .ForCtorParam(nameof(CartDto.Id), opt => opt.MapFrom(src => src.Id))
            .ForCtorParam(nameof(CartDto.Items), opt => opt.MapFrom(src => src.Items))
            .ForCtorParam(nameof(CartDto.Subtotal), opt => opt.MapFrom(src => src.Items.Sum(i => i.Quantity * i.UnitPriceSnapshot)));

        // Order Mappings
        CreateMap<OrderItem, OrderItemDto>()
            .ForCtorParam(nameof(OrderItemDto.ProductId), opt => opt.MapFrom(src => src.ProductId))
            .ForCtorParam(nameof(OrderItemDto.ProductName), opt => opt.MapFrom(src => src.ProductNameSnapshot))
            .ForCtorParam(nameof(OrderItemDto.UnitPrice), opt => opt.MapFrom(src => src.UnitPrice))
            .ForCtorParam(nameof(OrderItemDto.Quantity), opt => opt.MapFrom(src => src.Quantity))
            .ForCtorParam(nameof(OrderItemDto.TotalPrice), opt => opt.MapFrom(src => src.UnitPrice * src.Quantity));

        CreateMap<Order, OrderDto>()
            .ForCtorParam(nameof(OrderDto.Id), opt => opt.MapFrom(src => src.Id))
            .ForCtorParam(nameof(OrderDto.OrderNumber), opt => opt.MapFrom(src => src.OrderNumber))
            .ForCtorParam(nameof(OrderDto.TotalAmount), opt => opt.MapFrom(src => src.TotalAmount))
            .ForCtorParam(nameof(OrderDto.Status), opt => opt.MapFrom(src => src.Status.ToString()))
            .ForCtorParam(nameof(OrderDto.ShippingAddress), opt => opt.MapFrom(src => src.ShippingAddress))
            .ForCtorParam(nameof(OrderDto.CreatedAt), opt => opt.MapFrom(src => src.CreatedAtUtc))
            .ForCtorParam(nameof(OrderDto.Items), opt => opt.MapFrom(src => src.Items));
    }
}
