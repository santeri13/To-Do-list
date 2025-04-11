# To-Do-list
Santeri Pohjaranta Backend C# Developer Internship Assessment for Games Global

## Database
Database contain such structure:

tasks - This table contain tasks for all lists

| NAME     | DATATYPE     | DESCRIPTION      |
|----------|--------------|------------------|
| id       | INT          | id of task       |
| name     | VARCHAR(255) | task description |
| complete | BOOLEAN      | status of task   | 


sub_lists - contain sublists and their names

| NAME | DATATYPE     | DESCRIPTION  |
|------|--------------|--------------|
| id   | INT          | id list      |
| name | VARCHAR(256) | name of list |


tasks_in_list - contain connection between lists and tasks to know in which list task included. By default id 1 is main to-do-list, others are sublists.

| NAME    | DATATYPE | DESCRIPTION                     |
|---------|----------|---------------------------------|
| id      | INT      | id of record row                |
| list_id | INT      | id of list from sub_lists table |
| task_id | INT      | id of task from tasks table     |     

completed_tasks - include data about completed tasks 

| NAME         | DATATYPE | DESCRIPTION                 |
|--------------|----------|-----------------------------|
| id           | INT      | id of record row            |
| task_id      | INT      | id of task from tasks table |
| completed_at | DATETIME | time when task is completed |

More infromation about structure of database could be found in database_script.sql.

## Structure of API app

In **Contorllers/TaskController.cs** app is included all api request required by assesment base tasks.

**Done_Tasks.cs** and **Task.cs are** datastructre which is used to retrive data specifc way an dwork with it in list.

**Program.cs** contain code required for Swager work.

## API

**GetTaskList()** get full to-do-list with included sublists

**CreateNewList(string name)** create new list in sub_lists table.

**CreateNewTask(int list_id, string task_description)** create new task and include it in application. It require that api recive id of list in which it would be included and description of task. By default 1 is main to-do-list and others are sublists.

**UpdateTask(int task_id, string? task_description = null, bool? status = null)** update task status if it is done or not and task description. In request you can send only descripton change or status or both parameters with task id. If you do not like to change descripton or status of task include in place with this parameter null, as example: pdateTask(1, null, true).

**GetSubLists()** provide all sublists from sub_lists table and do not include main table by id 1.

 **GetSubList(int sub_list_id)** provide sublist from sub_lists by provided id.

 **GetDoneTasksRecord()** provide records about done tasks from completed_tasks table.

 **DeleteTask(int task_id)** delete task from tasks table and related with it information from other tables

 **DeleteSubList(int sub_list_id)** delete sublist from sub_lists table and tasks which was included in this table
