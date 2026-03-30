using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TgerCamera.Dtos;
using TgerCamera.Models;

namespace TgerCamera.Controllers;

[ApiController]
[Route("api/product-conditions")]
public class ProductConditionController : ControllerBase
{
    private readonly TgerCameraContext _context;
    private readonly IMapper _mapper;

    public ProductConditionController(TgerCameraContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProductConditionDto>>> GetAll()
    {
        var items = await _context.ProductConditions.ToListAsync();
        return Ok(_mapper.Map<IEnumerable<ProductConditionDto>>(items));
    }
}
