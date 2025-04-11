-- Table for main tasks
CREATE TABLE tasks (
    id INT AUTO_INCREMENT PRIMARY KEY, 
    name VARCHAR(255) NOT NULL,
    complete BOOLEAN DEFAULT FALSE
);

CREATE TABLE tasks_in_list(
	id INT AUTO_INCREMENT PRIMARY KEY, 
	list_id INT NOT NULL,
    task_id INT NOT NULL,
    FOREIGN KEY (list_id) REFERENCES sub_lists(id),
    FOREIGN KEY (task_id) REFERENCES tasks(id)
);

-- Table for sublists
CREATE TABLE sub_lists (
    id INT AUTO_INCREMENT PRIMARY KEY, 
    name varchar(256) NOT NULL
);

-- Optional table for tracking completed tasks
CREATE TABLE completed_tasks (
    id INT AUTO_INCREMENT PRIMARY KEY,
    task_id INT NOT NULL,
    completed_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (task_id) REFERENCES tasks(id)
);

insert into sub_lists (name) VALUES ("To-do-list");
insert into sub_lists (name) VALUES ("SubList1");
insert into sub_lists (name) VALUES ("SubList2");

insert into tasks (task,complete) VALUES ("task1",false);
insert into tasks (task,complete) VALUES ("task2",false);
insert into tasks (task,complete) VALUES ("task3",false);
insert into tasks (task,complete) VALUES ("task4",false);
insert into tasks (task,complete) VALUES ("task5",false);

insert into tasks_in_list (list_id,task_id) VALUES (1,1);
insert into tasks_in_list (list_id,task_id) VALUES (1,2);
insert into tasks_in_list(list_id,task_id) VALUES (2,3);
insert into tasks_in_list (list_id,task_id) VALUES (2,4);
insert into tasks_in_list (list_id,task_id) VALUES (3,5);

select tasks.name, tasks.complete, sub_lists.id, sub_lists.name as list_name from tasks_in_list join tasks on tasks_in_list.task_id = tasks.id join sub_lists on sub_lists.id = tasks_in_list.list_id where list_id = 1
