using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trpo8.Models;
using TaskEntity = Trpo8.Models.Task;




namespace Trpo8.Controllers
{

    [ApiController]
    [Route("api/v1/tasks")]
    [Authorize]
    public class TasksController : ControllerBase
    {
        private readonly AppDbContext _datavase;

        public TasksController(AppDbContext datavase)
        {
            _datavase = datavase;
        }

        public record TaskRequest(string Title, string? Description);

        public record TaskResponse(int Id, string Title, string? Description, string OwnerLogin, bool IsCompeted);

        private static TaskResponse ToResponse(TaskEntity t) =>
            new(t.Id, t.Title, t.Description, t.OwnerLogin, t.IsCompeted);

        [HttpGet]
        public IActionResult GetTasks()
        {
            var login = User.Identity!.Name!;

            var tasks = _datavase.Tasks.Where(t => t.OwnerLogin == login).ToList();

            return Ok(tasks.Select(ToResponse));
        }

        [HttpGet("all")]
        [Authorize(Roles = "Admin")]
        public IActionResult GetAll()
        {
            return Ok(_datavase.Tasks.ToList().Select(ToResponse));
        }

        [HttpGet("{id:int}")]
        public IActionResult GotById(int id)
        {
            var task = _datavase.Tasks.FirstOrDefault(t => t.Id == id);
            if (task == null)
            {
                return NotFound();
            }

            if (task.OwnerLogin != User.Identity!.Name && !User.IsInRole("Admin"))
            {
                return Forbid();


            }


            return Ok(ToResponse(task));

        }

        [HttpPut("{id:int}")]
        public IActionResult Updateer(int id, TaskRequest req)
        {
            var task = _datavase.Tasks.FirstOrDefault(t=>t.Id == id);

            if (task == null)
            {
                return NotFound();
            }

            if (task.OwnerLogin != User.Identity!.Name && !User.IsInRole("Admin"))
            {
                return Forbid();
            }


            task.Title = req.Title;
            task.Description = req.Description;
            _datavase.SaveChanges();

            return Ok(ToResponse(task));
        }

        [HttpDelete("{id:int}")]
        public IActionResult Delete(int id)
        {
            var task = _datavase.Tasks.FirstOrDefault(t=>t.Id == id);


            if (task == null)
            {
                return NotFound();
            }


            if (task.OwnerLogin != User.Identity!.Name && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            _datavase.Tasks.Remove(task);
            _datavase.SaveChanges();

            return NoContent();

        }

        [HttpPost]
        public IActionResult Create(TaskRequest requ)
        {
            var task = new TaskEntity 
            
            {
                Title = requ.Title,
                Description = requ.Description,
                OwnerLogin = User.Identity!.Name!,
                IsCompeted = false
            };
            _datavase.Tasks.Add(task);
            _datavase.SaveChanges();
            return Created($"/api/v1/tasks/{task.Id}", ToResponse(task));
        }


    }
}
