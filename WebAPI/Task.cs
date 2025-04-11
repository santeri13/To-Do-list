namespace WebAPI
{
    public class Task
    {
        public int id { get; set; }
        public string task { get; set; }
        public Boolean complete { get; set; }
        public List<Task> sub_Lists { get; set; } = new List<Task>();
    }

}
