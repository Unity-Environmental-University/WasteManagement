using System.Collections.Generic;
using _project.Scripts.Object_Scripts;
using UnityEngine;

namespace _project.Scripts.Core
{
    public class IssuePreview : MonoBehaviour
    {
        public List<IssueObject> upcomingIssues;

        public List<IssueObject> GetUpcomingIssues()
        {
            return upcomingIssues is { Count: > 0 } ? upcomingIssues : GenerateUpcoming();
        }

        private static List<IssueObject> GenerateUpcoming()
        {
            var newList = new List<IssueObject>();
            //TODO Generate a list of IssueObjects

            return newList;
        }
    }
}