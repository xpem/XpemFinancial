using System;
using System.Collections.Generic;
using System.Text;

namespace Model.Resp
{
    public class ServiceResp(bool success, object? content = null)
    {
        public bool Success { get; set; } = success;

        public object? Content { get; set; } = content;
    }
}
