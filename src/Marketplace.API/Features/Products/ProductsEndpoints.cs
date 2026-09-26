using Marketplace.API.Features.Products.Create;
using Marketplace.API.Features.Products.Delete;
using Marketplace.API.Features.Products.DeleteImage;
using Marketplace.API.Features.Products.GetBySlug;
using Marketplace.API.Features.Products.My;
using Marketplace.API.Features.Products.Search;
using Marketplace.API.Features.Products.Update;
using Marketplace.API.Features.Products.UploadImage;

namespace Marketplace.API.Features.Products;

public static class ProductsEndpoints
{
    public static IEndpointRouteBuilder MapProductsEndpoints(this IEndpointRouteBuilder app)
    {
        SearchProducts.Map(app);
        GetProductBySlug.Map(app);
        MyProducts.Map(app);
        CreateProduct.Map(app);
        UpdateProduct.Map(app);
        DeleteProduct.Map(app);
        UploadProductImage.Map(app);
        DeleteProductImage.Map(app);
        return app;
    }
}
