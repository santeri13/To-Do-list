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
                new SqlDataAdapter("INSERT INTO sub_lists (name) VALUES ("+ name + ")", con);
                
                return Ok("New sublist created");

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
                SqlConnection con = new SqlConnection(_configuration.GetConnectionString("WebAPIContext").ToString());
                new SqlDataAdapter("INSERT INTO tasks (name, complete) VALUES ("+ task_description + ", "+false+")", con);
                SqlDataAdapter get_last_id = new SqlDataAdapter("SELECT LAST_INSERT_ID();", con);

                int newTaskId = Convert.ToInt32(get_last_id.SelectCommand.ExecuteScalar());

                new SqlDataAdapter("INSERT INTO tasks_in_list (task_id, list_id) VALUES ("+ newTaskId + ", "+ list_id + ")", con);

                // Return the ID of the newly created task
                return Ok("New task added to list");
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "An error occurred while creating the task.");
                return StatusCode(500, "Internal server error");
            }

        }

        // Task 4/7 Implement update operations within items (e.g., update task description, status)./ Allow marking tasks as done
        [HttpPost]
        [Route("UpdateTask")]
        public IActionResult CreateNewTask(int task_id, string task_description, Boolean status) {

            string answer = "";
            if (task_description != null)
            {
                string myConnectionString = "server=localhost;uid=root;pwd=Samulipoh051998;database=to-do-list";
                using MySqlConnection myConnection = new MySqlConnection(myConnectionString);
                myConnection.Open();

                using MySqlCommand update_task = new MySqlCommand();
                update_task.Connection = myConnection;
                update_task.CommandText = @"UPDATE tasks SET name = @name WHERE id = @id";
                update_task.Parameters.AddWithValue("@name", task_description);
                update_task.Parameters.AddWithValue("@id", task_id);

                // Execute the insert command
                update_task.ExecuteNonQuery();
                myConnection.Close();

                answer=answer+"Task description updated;";
            }
            if (status != null) {
                string myConnectionString = "server=localhost;uid=root;pwd=Samulipoh051998;database=to-do-list";
                using MySqlConnection myConnection = new MySqlConnection(myConnectionString);
                myConnection.Open();

                using MySqlCommand update_status = new MySqlCommand();
                update_status.Connection = myConnection;
                update_status.CommandText = @"UPDATE tasks SET name = @name WHERE id = @id";
                update_status.Parameters.AddWithValue("@complete", status);
                update_status.Parameters.AddWithValue("@id", task_id);
                update_status.ExecuteNonQuery();

                using MySqlCommand update_done_task = new MySqlCommand();
                update_done_task.Connection = myConnection;

                if (status == true)
                {
                    update_status.CommandText = @"INSERT INTO completed_tasks (task_id, completed_at) VALUES (@task_id, @completed_at)";
                    update_status.Parameters.AddWithValue("@task_id", task_id);
                    update_status.Parameters.AddWithValue("@completed_at", DateTime.Now);
                    update_status.ExecuteNonQuery();

                }
                else
                {
                    update_status.CommandText = @"DELETE FROM completed_tasks WHERE task_id = @task_id";
                    update_status.Parameters.AddWithValue("@task_id", task_id);
                    update_status.ExecuteNonQuery();
                }

                // Execute the insert command
                myConnection.Close();

                answer = answer + "Task status updated;";

            }

            return Ok(answer);
        }

        // Task 5 Enable listing available sub lists and their tasks.
        [HttpGet]
        [Route("GetSubLists")]
        public IEnumerable GetSubLists(int task_id)
        {
            string myConnectionString = "server=localhost;uid=root;pwd=Samulipoh051998;database=to-do-list";
            MySqlConnection myConnection = new MySqlConnection(myConnectionString);
            //open a connection
            myConnection.Open();

            // create a MySQL command and set the SQL statement with parameters

            MySqlCommand retrive_list = new MySqlCommand();
            retrive_list.Connection = myConnection;
            retrive_list.CommandText = @"select tasks.name, sub_lists.id, sub_lists.name as list_name from tasks_in_list 
                                        join tasks on tasks_in_list.task_id = tasks.id 
                                        join sub_lists on sub_lists.id = tasks_in_list.list_id where sub_lists.id != 1";

            using var listReader = retrive_list.ExecuteReader();
            ArrayList list = new ArrayList();
            List<Task> subList = new List<Task>();
            Task task = new Task();
            int current_list_id = 0;

            while (listReader.Read())
            {
                
                if (current_list_id != listReader.GetInt32("id"))
                {
                    if (subList != null)
                    {
                        list.Add(subList);
                    }
                    subList = new List<Task>();
                    task = new Task();
                    task.id = listReader.GetInt32("id");
                    task.task = listReader.GetString("name");
                    task.complete = listReader.GetBoolean("complete");
                    subList.Add(task);
                }
                else
                {
                    task = new Task();
                    task.id = listReader.GetInt32("id");
                    task.task = listReader.GetString("name");
                    task.complete = listReader.GetBoolean("complete");
                    subList.Add(task);
                }
                
            }

            myConnection.Close();

            return list.ToArray();
        }

        // Task 6 Implement the ability to retrieve a specific list with its tasks.
        [HttpGet]
        [Route("GetSubList")]
        public IEnumerable GetSubList(int sub_list_id) {
            string myConnectionString = "server=localhost;uid=root;pwd=Samulipoh051998;database=to-do-list";
            MySqlConnection myConnection = new MySqlConnection(myConnectionString);
            //open a connection
            myConnection.Open();

            // create a MySQL command and set the SQL statement with parameters

            MySqlCommand get_sub_list = new MySqlCommand();
            get_sub_list.Connection = myConnection;
            get_sub_list.CommandText = @"select tasks.name, sub_lists.id, sub_lists.name as list_name from tasks_in_list 
                                        join tasks on tasks_in_list.task_id = tasks.id 
                                        join sub_lists on sub_lists.id = tasks_in_list.list_id where sub_lists.id = @sub_list_id";
            get_sub_list.Parameters.AddWithValue("@sub_list_id", sub_list_id);
            using var listReader = get_sub_list.ExecuteReader();
            List<Task> subList = new List<Task>();

            while (listReader.Read())
            {
                Task task = new Task();
                task.id = listReader.GetInt32("id");
                task.task = listReader.GetString("name");
                task.complete = listReader.GetBoolean("complete");
                subList.Add(task);
            }

            myConnection.Close();

            return subList.ToArray();

        }

        // Task 8  Provide a way to view the history of done tasks.
        [HttpGet]
        [Route("GetDoneTasksRecord")]
        public IEnumerable<Done_Tasks> GetDoneTasksRecord()
        {
            string myConnectionString = "server=localhost;uid=root;pwd=Samulipoh051998;database=to-do-list";
            MySqlConnection myConnection = new MySqlConnection(myConnectionString);
            try
            {
                //open a connection
                myConnection.Open();

                MySqlCommand get_sub_list = new MySqlCommand();
                get_sub_list.Connection = myConnection;
                get_sub_list.CommandText = @"SELECT tasks.name, completed_tasks.completed_at from completed_tasks
                                            JOIN tasks ON completed_tasks.task_id = tasks.id";

                using var listReader = get_sub_list.ExecuteReader();
                List<Done_Tasks> doneTasksList = new List<Done_Tasks>();

                while (listReader.Read()) {
                    Done_Tasks task = new Done_Tasks();
                    task.description = listReader.GetString("name");
                    task.completed_at = listReader.GetDateTime("name");
                    doneTasksList.Add(task);
                }

                myConnection.Close();

                return doneTasksList.ToArray();
            }
            catch (MySqlException ex)
            {
                _logger.LogError(ex, "An error occurred while adding the task.");
                return (IEnumerable<Done_Tasks>)StatusCode(500, "Internal server error");
            }
            finally
            {
                // Close the connection if it was opened
                myConnection.Close();
            }
        }

        // Task 9 Implement deletion functionality for tasks and sub lists.
        [HttpPost]
        [Route("DeleteTask")]
        public IActionResult DeleteTask(int task_id)
        {
            string myConnectionString = "server=localhost;uid=root;pwd=Samulipoh051998;database=to-do-list";
            MySqlConnection myConnection = new MySqlConnection(myConnectionString);
            try
            {
                myConnection.Open();

                MySqlCommand delet_task = new MySqlCommand();
                delet_task.Connection = myConnection;
                delet_task.CommandText = @"DELETE FROM tasks_in_list WHERE task_id = @task_id";
                delet_task.Parameters.AddWithValue("@task_id", task_id);
                delet_task.ExecuteNonQuery();

                delet_task.CommandText = @"DELETE FROM tasks WHERE id = @task_id";
                delet_task.Parameters.AddWithValue("@task_id", task_id);
                delet_task.ExecuteNonQuery();

                myConnection.Close();

                return Ok("Task deleted");
            }
            catch (MySqlException ex)
            {
                _logger.LogError(ex, "An error occurred while adding the task.");
                return StatusCode(500, "Internal server error");
            }
            finally
            {
                // Close the connection if it was opened
                myConnection.Close();
            }
        }

        [HttpPost]
        [Route("DeleteSubList")]
        public IActionResult DeleteSubList(int sub_list_id)
        {
            string myConnectionString = "server=localhost;uid=root;pwd=Samulipoh051998;database=to-do-list";
            MySqlConnection myConnection = new MySqlConnection(myConnectionString);
            try
            {
                //open a connection
                myConnection.Open();

                // create a MySQL command and set the SQL statement with parameters

                MySqlCommand get_sub_list = new MySqlCommand();
                get_sub_list.Connection = myConnection;
                get_sub_list.CommandText = @"select tasks.id from tasks_in_list 
                                        join tasks on tasks_in_list.task_id = tasks.id 
                                        join sub_lists on sub_lists.id = tasks_in_list.list_id where sub_lists.id = @sub_list_id";

                get_sub_list.Parameters.AddWithValue("@sub_list_id", sub_list_id);
                using var listReader = get_sub_list.ExecuteReader();

                MySqlCommand delete_from_complete_tasks = new MySqlCommand();
                delete_from_complete_tasks.Connection = myConnection;
                delete_from_complete_tasks.CommandText = @"DELETE FROM complete_tasks where task_id=@task_id";

                MySqlCommand delete_from_task_list = new MySqlCommand();
                delete_from_task_list.Connection = myConnection;
                delete_from_task_list.CommandText = @"DELETE FROM complete_tasks where task_id=@task_id";

                MySqlCommand delete_from_tasks = new MySqlCommand();
                delete_from_tasks.Connection = myConnection;
                delete_from_tasks.CommandText = @"DELETE FROM tasks where id=@task_id";

                while (listReader.Read())
                {
                    delete_from_complete_tasks.Parameters.AddWithValue("@task_id", listReader.GetInt32("id"));
                    delete_from_complete_tasks.ExecuteNonQuery();

                    delete_from_task_list.Parameters.AddWithValue("@task_id", listReader.GetInt32("id"));
                    delete_from_task_list.ExecuteNonQuery();

                    delete_from_tasks.Parameters.AddWithValue("@task_id", listReader.GetInt32("id"));
                    delete_from_tasks.ExecuteNonQuery();

                }

                MySqlCommand delete_from_sub_lists = new MySqlCommand();
                delete_from_sub_lists.Connection = myConnection;
                delete_from_sub_lists.CommandText = @"DELETE FROM tasks_in_list where id=@task_id";
                delete_from_sub_lists.Parameters.AddWithValue("@task_id", sub_list_id);
                delete_from_sub_lists.ExecuteNonQuery();

                myConnection.Close();

                return Ok("Sublist deleted");
            }
            catch (MySqlException ex)
            {
                _logger.LogError(ex, "An error occurred while adding the task.");
                return StatusCode(500, "Internal server error");
            }
            finally
            {
                // Close the connection if it was opened
                myConnection.Close();
            }
        }
    }
}
