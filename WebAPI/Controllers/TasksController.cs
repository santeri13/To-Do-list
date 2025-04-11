using System.Collections;
using System.Data;
using System.Data.Common;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using MySql.Data.MySqlClient;
using MySqlX.XDevAPI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Asn1.Cms;


namespace WebAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TasksController : ControllerBase
    {
        private readonly ILogger<TasksController> _logger;
        public readonly IConfiguration _configuration;

        public TasksController(ILogger<TasksController> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        // Task 1 Develop a WebApi for a To-Do list
        [HttpGet]
        [Route("GetTaskList")]
        public IEnumerable GetTaskList()
        {
            SqlConnection con = new SqlConnection(_configuration.GetConnectionString("WebAPIContext").ToString());
            con.Open();

            SqlDataAdapter da = new SqlDataAdapter("select tasks.name, sub_lists.id, sub_lists.name as list_name from tasks_in_list " +
                                                    "join tasks on tasks_in_list.task_id = tasks.id " +
                                                    "join sub_lists on sub_lists.id = tasks_in_list.list_id", con);
            DataTable dt = new DataTable();
            da.Fill(dt);
            
            ArrayList list = new ArrayList();
            List<Task> subList = new List<Task>();
            Task task = new Task();
            int current_list_id = 0;

            foreach (DataRow row in dt.Rows)
            {
                if (Convert.ToInt32(row["id"]) == 1)
                {
                    task = new Task();
                    task.id = Convert.ToInt32(row["id"]);
                    task.task = row["name"].ToString();
                    list.Add(task);
                }
                else
                {
                    if (current_list_id  != Convert.ToInt32(row["id"]))
                    {
                        if (subList != new List<Task>())
                        {
                            list.Add(subList);
                        }
                        subList = new List<Task>();
                        task = new Task();
                        task.id = Convert.ToInt32(row["id"]);
                        task.task = row["list_name"].ToString();
                        subList.Add(task);
                    }
                    else
                    {
                        task = new Task();
                        task.id = Convert.ToInt32(row["id"]);
                        task.task = row["list_name"].ToString();
                        subList.Add(task);
                    }
                }
            }

            return list.ToArray();
        }

        // Task 2 Implement the ability to create multiple "sub lists" within the main To-Do list
        [HttpPost]
        [Route("CreateNewList")]
        public IActionResult CreateNewList(string name){
            try
            {
                SqlConnection con = new SqlConnection(_configuration.GetConnectionString("WebAPIContext").ToString());
                con.Open();
                using SqlCommand createNewListCommand = new SqlCommand("INSERT INTO sub_lists (name) VALUES (@name)", con);
                createNewListCommand.Parameters.AddWithValue("@name", name);

                int rowsAffected = createNewListCommand.ExecuteNonQuery();

                if (rowsAffected > 0)
                {
                    return Ok("New sublist created.");
                }
                else
                {
                    return StatusCode(500, "Failed to create the sublist.");
                }

            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "An error occurred while adding the task.");
                return StatusCode(500, "Internal server error");
            }
        }

        // Task 3 Enable adding new items to any sub list, or the “default” main To-Do list if none is specified.
        [HttpPost]
        [Route("CreateNewTask")]
        public IActionResult CreateNewTask(int list_id, string task_description) {
            try
            {
                // Retrieve the connection string from the configuration
                string connectionString = _configuration.GetConnectionString("WebAPIContext").ToString();

                // Open a connection to the database
                using SqlConnection con = new SqlConnection(connectionString);
                con.Open();

                // Insert the new task into the tasks table
                string insertTaskQuery = "INSERT INTO tasks (name, complete) VALUES (@name, @complete); SELECT SCOPE_IDENTITY();";
                using SqlCommand insertTaskCommand = new SqlCommand(insertTaskQuery, con);
                insertTaskCommand.Parameters.AddWithValue("@name", task_description);
                insertTaskCommand.Parameters.AddWithValue("@complete", false);

                // Execute the query and retrieve the new task ID
                int newTaskId = Convert.ToInt32(insertTaskCommand.ExecuteScalar());

                // Associate the task with the specified list
                string associateTaskQuery = "INSERT INTO tasks_in_list (task_id, list_id) VALUES (@task_id, @list_id)";
                using SqlCommand associateTaskCommand = new SqlCommand(associateTaskQuery, con);
                associateTaskCommand.Parameters.AddWithValue("@task_id", newTaskId);
                associateTaskCommand.Parameters.AddWithValue("@list_id", list_id);
                associateTaskCommand.ExecuteNonQuery();

                // Return the ID of the newly created task
                return Ok("New task added to list");
            }
            catch (SqlException ex)
            {
                // Log the error and return a 500 status code
                _logger.LogError(ex, "An error occurred while creating the task.");
                return StatusCode(500, "Internal server error.");
            }

        }

        // Task 4/7 Implement update operations within items (e.g., update task description, status)./ Allow marking tasks as done
        [HttpPost]
        [Route("UpdateTask")]
        public IActionResult UpdateTask(int task_id, string? task_description = null, bool? status = null) {

            try
    {
        // Retrieve the connection string from the configuration
        string connectionString = _configuration.GetConnectionString("WebAPIContext").ToString();

        // Open a connection to the database
        using SqlConnection con = new SqlConnection(connectionString);
        con.Open();

        // Update the task description if provided
        if (!string.IsNullOrEmpty(task_description))
        {
            string updateDescriptionQuery = "UPDATE tasks SET name = @name WHERE id = @id";
            using SqlCommand updateDescriptionCommand = new SqlCommand(updateDescriptionQuery, con);
            updateDescriptionCommand.Parameters.AddWithValue("@name", task_description);
            updateDescriptionCommand.Parameters.AddWithValue("@id", task_id);
            updateDescriptionCommand.ExecuteNonQuery();
        }

        // Update the task status if provided
        if (status.HasValue)
        {
            string updateStatusQuery = "UPDATE tasks SET complete = @complete WHERE id = @id";
            using SqlCommand updateStatusCommand = new SqlCommand(updateStatusQuery, con);
            updateStatusCommand.Parameters.AddWithValue("@complete", status.Value);
            updateStatusCommand.Parameters.AddWithValue("@id", task_id);
            updateStatusCommand.ExecuteNonQuery();

            // Update the completed_tasks table
            if (status.Value)
            {
                string insertCompletedTaskQuery = "INSERT INTO completed_tasks (task_id, completed_at) VALUES (@task_id, @completed_at)";
                using SqlCommand insertCompletedTaskCommand = new SqlCommand(insertCompletedTaskQuery, con);
                insertCompletedTaskCommand.Parameters.AddWithValue("@task_id", task_id);
                insertCompletedTaskCommand.Parameters.AddWithValue("@completed_at", DateTime.Now);
                insertCompletedTaskCommand.ExecuteNonQuery();
            }
            else
            {
                string deleteCompletedTaskQuery = "DELETE FROM completed_tasks WHERE task_id = @task_id";
                using SqlCommand deleteCompletedTaskCommand = new SqlCommand(deleteCompletedTaskQuery, con);
                deleteCompletedTaskCommand.Parameters.AddWithValue("@task_id", task_id);
                deleteCompletedTaskCommand.ExecuteNonQuery();
            }
        }

        // Return a success response
        return Ok("Task updated successfully.");
            }
            catch (SqlException ex)
            {
                // Log the error and return a 500 status code
                _logger.LogError(ex, "An error occurred while updating the task.");
                return StatusCode(500, "Internal server error.");
            }
        }

        // Task 5 Enable listing available sub lists and their tasks.
        [HttpGet]
        [Route("GetSubLists")]
        public IActionResult GetSubLists()
        {
            try
            {
                // Retrieve the connection string from the configuration
                string connectionString = _configuration.GetConnectionString("WebAPIContext");

                // Open a connection to the database
                using SqlConnection con = new SqlConnection(connectionString);
                con.Open();

                // Define the SQL query to retrieve sublists and their associated tasks
                string query = @"
                SELECT 
                    sl.id AS SubListId, 
                    sl.name AS SubListName, 
                    t.id AS TaskId, 
                    t.name AS TaskName, 
                    t.complete AS TaskComplete
                FROM sub_lists sl
                LEFT JOIN tasks_in_list til ON sl.id = til.list_id
                LEFT JOIN tasks t ON til.task_id = t.id";

                // Execute the query
                using SqlCommand cmd = new SqlCommand(query, con);
                using SqlDataReader reader = cmd.ExecuteReader();

                // Create a dictionary to group sublists and their tasks
                var subLists = new Dictionary<int, object>();

                while (reader.Read())
                {
                    int subListId = reader.GetInt32(reader.GetOrdinal("SubListId"));
                    string subListName = reader.GetString(reader.GetOrdinal("SubListName"));

                    // Check if the sublist already exists in the dictionary
                    if (!subLists.ContainsKey(subListId))
                    {
                        subLists[subListId] = new
                        {
                            SubListId = subListId,
                            SubListName = subListName,
                            Tasks = new List<object>()
                        };
                    }

                    // Add the task to the sublist
                    if (!reader.IsDBNull(reader.GetOrdinal("TaskId")))
                    {
                        ((List<object>)((dynamic)subLists[subListId]).Tasks).Add(new
                        {
                            TaskId = reader.GetInt32(reader.GetOrdinal("TaskId")),
                            TaskName = reader.GetString(reader.GetOrdinal("TaskName")),
                            TaskComplete = reader.GetBoolean(reader.GetOrdinal("TaskComplete"))
                        });
                    }
                }

                // Return the sublists as a JSON response
                return Ok(subLists.Values);
            }
            catch (SqlException ex)
            {
                // Log the error and return a 500 status code
                _logger.LogError(ex, "An error occurred while fetching the sublists.");
                return StatusCode(500, "Internal server error.");
            }
        }

        // Task 6 Implement the ability to retrieve a specific list with its tasks.
        [HttpGet]
        [Route("GetSubList")]
        public IActionResult GetSubList(int sub_list_id) {
            try
            {
                // Retrieve the connection string from the configuration
                string connectionString = _configuration.GetConnectionString("WebAPIContext").ToString();

                // Open a connection to the database
                using SqlConnection con = new SqlConnection(connectionString);
                con.Open();

                // Define the SQL query to retrieve the sublist and its associated tasks
                string query = @"
                SELECT 
                    sl.id AS SubListId, 
                    sl.name AS SubListName, 
                    t.id AS TaskId, 
                    t.name AS TaskName, 
                    t.complete AS TaskComplete
                FROM sub_lists sl
                LEFT JOIN tasks_in_list til ON sl.id = til.list_id
                LEFT JOIN tasks t ON til.task_id = t.id
                WHERE sl.id = @sub_list_id";

                // Execute the query
                using SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@sub_list_id", sub_list_id);
                using SqlDataReader reader = cmd.ExecuteReader();

                // Create an object to store the sublist and its tasks
                var subList = new
                {
                    SubListId = sub_list_id,
                    SubListName = string.Empty,
                    Tasks = new List<object>()
                };

                while (reader.Read())
                {
                    // Set the sublist name
                    if (string.IsNullOrEmpty(subList.SubListName))
                    {
                        subList = new
                        {
                            SubListId = sub_list_id,
                            SubListName = reader.GetString(reader.GetOrdinal("SubListName")),
                            Tasks = subList.Tasks
                        };
                    }

                    // Add the task to the sublist
                    if (!reader.IsDBNull(reader.GetOrdinal("TaskId")))
                    {
                        subList.Tasks.Add(new
                        {
                            TaskId = reader.GetInt32(reader.GetOrdinal("TaskId")),
                            TaskName = reader.GetString(reader.GetOrdinal("TaskName")),
                            TaskComplete = reader.GetBoolean(reader.GetOrdinal("TaskComplete"))
                        });
                    }
                }

                // Return the sublist as a JSON response
                return Ok(subList);
            }
            catch (SqlException ex)
            {
                // Log the error and return a 500 status code
                _logger.LogError(ex, "An error occurred while fetching the sublist.");
                return StatusCode(500, "Internal server error.");
            }

        }

        // Task 8  Provide a way to view the history of done tasks.
        [HttpGet]
        [Route("GetDoneTasksRecord")]
        public IActionResult GetDoneTasksRecord()
        {
            try
            {
                // Retrieve the connection string from the configuration
                string connectionString = _configuration.GetConnectionString("WebAPIContext").ToString();

                // Open a connection to the database
                using SqlConnection con = new SqlConnection(connectionString);
                con.Open();

                // Define the SQL query to retrieve completed tasks
                string query = @"
                SELECT 
                    t.name AS TaskDescription, 
                    ct.completed_at AS CompletedAt
                FROM completed_tasks ct
                JOIN tasks t ON ct.task_id = t.id";

                // Execute the query
                using SqlCommand cmd = new SqlCommand(query, con);
                using SqlDataReader reader = cmd.ExecuteReader();

                // Create a list to store the completed tasks
                var doneTasksList = new List<Done_Tasks>();

                while (reader.Read())
                {
                    // Map the data to the Done_Tasks object
                    doneTasksList.Add(new Done_Tasks
                    {
                        description = reader.GetString(reader.GetOrdinal("TaskDescription")),
                        completed_at = reader.GetDateTime(reader.GetOrdinal("CompletedAt"))
                    });
                }

                // Return the completed tasks as a JSON response
                return Ok(doneTasksList);
            }
            catch (SqlException ex)
            {
                // Log the error and return a 500 status code
                _logger.LogError(ex, "An error occurred while fetching the completed tasks.");
                return StatusCode(500, "Internal server error.");
            }
        }

        // Task 9 Implement deletion functionality for tasks and sub lists.
        [HttpPost]
        [Route("DeleteTask")]
        public IActionResult DeleteTask(int task_id)
        {
            try
            {
                // Retrieve the connection string from the configuration
                string connectionString = _configuration.GetConnectionString("WebAPIContext").ToString();

                // Open a connection to the database
                using MySqlConnection con = new MySqlConnection(connectionString);
                con.Open();

                // Begin a transaction
                using var transaction = con.BeginTransaction();

                // Delete from completed_tasks
                string deleteCompletedTasksQuery = "DELETE FROM completed_tasks WHERE task_id = @task_id";
                using MySqlCommand deleteCompletedTasksCommand = new MySqlCommand(deleteCompletedTasksQuery, con, transaction);
                deleteCompletedTasksCommand.Parameters.AddWithValue("@task_id", task_id);
                deleteCompletedTasksCommand.ExecuteNonQuery();

                // Delete from tasks_in_list
                string deleteTasksInListQuery = "DELETE FROM tasks_in_list WHERE task_id = @task_id";
                using MySqlCommand deleteTasksInListCommand = new MySqlCommand(deleteTasksInListQuery, con, transaction);
                deleteTasksInListCommand.Parameters.AddWithValue("@task_id", task_id);
                deleteTasksInListCommand.ExecuteNonQuery();

                // Delete from tasks
                string deleteTasksQuery = "DELETE FROM tasks WHERE id = @task_id";
                using MySqlCommand deleteTasksCommand = new MySqlCommand(deleteTasksQuery, con, transaction);
                deleteTasksCommand.Parameters.AddWithValue("@task_id", task_id);
                int rowsAffected = deleteTasksCommand.ExecuteNonQuery();

                // Commit the transaction
                transaction.Commit();

                // Check if the task was deleted
                if (rowsAffected > 0)
                {
                    return Ok(new { message = "Task deleted successfully.", task_id });
                }
                else
                {
                    return NotFound(new { message = "Task not found.", task_id });
                }
            }
            catch (MySqlException ex)
            {
                // Log the error and return a 500 status code
                _logger.LogError(ex, "An error occurred while deleting the task.");
                return StatusCode(500, "Internal server error.");
            }
        }

        [HttpPost]
        [Route("DeleteSubList")]
        public IActionResult DeleteSubList(int sub_list_id)
        {
            try
            {
                // Retrieve the connection string from the configuration
                string connectionString = _configuration.GetConnectionString("WebAPIContext");

                // Open a connection to the database
                using MySqlConnection con = new MySqlConnection(connectionString);
                con.Open();

                // Begin a transaction
                using var transaction = con.BeginTransaction();

                // Delete associated tasks from completed_tasks
                string deleteCompletedTasksQuery = @"
                DELETE ct 
                FROM completed_tasks ct
                JOIN tasks_in_list til ON ct.task_id = til.task_id
                WHERE til.list_id = @sub_list_id";
                using MySqlCommand deleteCompletedTasksCommand = new MySqlCommand(deleteCompletedTasksQuery, con, transaction);
                deleteCompletedTasksCommand.Parameters.AddWithValue("@sub_list_id", sub_list_id);
                deleteCompletedTasksCommand.ExecuteNonQuery();

                // Delete associated tasks from tasks_in_list
                string deleteTasksInListQuery = "DELETE FROM tasks_in_list WHERE list_id = @sub_list_id";
                using MySqlCommand deleteTasksInListCommand = new MySqlCommand(deleteTasksInListQuery, con, transaction);
                deleteTasksInListCommand.Parameters.AddWithValue("@sub_list_id", sub_list_id);
                deleteTasksInListCommand.ExecuteNonQuery();

                // Delete the sublist itself
                string deleteSubListQuery = "DELETE FROM sub_lists WHERE id = @sub_list_id";
                using MySqlCommand deleteSubListCommand = new MySqlCommand(deleteSubListQuery, con, transaction);
                deleteSubListCommand.Parameters.AddWithValue("@sub_list_id", sub_list_id);
                int rowsAffected = deleteSubListCommand.ExecuteNonQuery();

                // Commit the transaction
                transaction.Commit();

                // Check if the sublist was deleted
                if (rowsAffected > 0)
                {
                    return Ok(new { message = "Sublist deleted successfully.", sub_list_id });
                }
                else
                {
                    return NotFound(new { message = "Sublist not found.", sub_list_id });
                }
            }
            catch (MySqlException ex)
            {
                // Log the error and return a 500 status code
                _logger.LogError(ex, "An error occurred while deleting the sublist.");
                return StatusCode(500, "Internal server error.");
            }
        }
    }
}
