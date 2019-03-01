using System.Collections.Generic;

namespace UiPathWorkflow
{
    public class UiPathStartInfo
    {
        public string InputArguments { get; set; }
        public int JobsCount { get; set; }
        public string ReleaseKey { get; set; }
        public string Strategy { get; set; }
        public List<int> RobotIds { get; set; }
    }
}