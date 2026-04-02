using AutoMapper;
using TgerCamera.Models;
using TgerCamera.Dtos;
using System.Linq;

namespace TgerCamera.Mapping;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // Product mappings
        CreateMap<Product, ProductDto>()
            .ForMember(dest => dest.Brand, opt => opt.MapFrom(src => src.Brand))
            .ForMember(dest => dest.Category, opt => opt.MapFrom(src => src.Category))
            .ForMember(dest => dest.Condition, opt => opt.MapFrom(src => src.Condition))
            .ForMember(dest => dest.Specifications, opt => opt.MapFrom(src => src.ProductSpecifications))
            .ForMember(dest => dest.MainImageUrl, opt => opt.Ignore())
            .AfterMap((src, dest) =>
            {
                if (src.ProductImages != null && src.ProductImages.Any(pi => pi.IsMain == true))
                {
                    dest.MainImageUrl = src.ProductImages.First(pi => pi.IsMain == true).ImageUrl;
                }
                else if (src.ProductImages != null && src.ProductImages.Any())
                {
                    dest.MainImageUrl = src.ProductImages.First().ImageUrl;
                }
            });

        CreateMap<ProductDto, Product>();

        CreateMap<CreateProductRequestDto, Product>()
            .ForMember(dest => dest.ProductSpecifications, opt => opt.MapFrom(src => src.Specifications))
            .ForMember(dest => dest.ProductImages, opt => opt.Ignore());

        CreateMap<ProductSpecificationCreateDto, ProductSpecification>();

        CreateMap<ProductSpecification, ProductSpecificationDto>();
        CreateMap<ProductSpecificationDto, ProductSpecification>();

        // RentalProduct mappings
        CreateMap<RentalProduct, RentalProductDto>();
        CreateMap<RentalProductDto, RentalProduct>();

        // User mappings
        CreateMap<User, UserDto>();
        CreateMap<UserDto, User>();

        // Wishlist mappings
        CreateMap<Wishlist, WishlistDto>();
        CreateMap<WishlistDto, Wishlist>();

        // Category, Brand, Condition mappings
        CreateMap<Category, CategoryDto>();
        CreateMap<CategoryDto, Category>();

        CreateMap<Brand, BrandDto>();
        CreateMap<BrandDto, Brand>();

        CreateMap<ProductCondition, ProductConditionDto>();
        CreateMap<ProductConditionDto, ProductCondition>();

        // Cart mappings
        CreateMap<Cart, Dtos.CartDto>()
            .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src.CartItems));
        CreateMap<Dtos.CartDto, Cart>()
            .ForMember(dest => dest.CartItems, opt => opt.MapFrom(src => src.Items));
        CreateMap<CartItem, Dtos.CartItemDto>()
            .ForMember(dest => dest.Product, opt => opt.MapFrom(src => src.Product));
        CreateMap<Dtos.CartItemDto, CartItem>();

        // Order mappings
        CreateMap<Order, Dtos.Order.OrderDto>()
            .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src.OrderItems));
        CreateMap<Dtos.Order.OrderDto, Order>();
        CreateMap<OrderItem, Dtos.Order.OrderItemDto>();
        CreateMap<Dtos.Order.OrderItemDto, OrderItem>();
    }
}
