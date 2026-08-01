using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DemoService;
using DemoService.Controllers;

namespace DemoServiceTests;

[TestClass]
public sealed class HashControllerTests
{
    [TestMethod]
    public void Get_ReturnsHtmlContent()
    {
        // Arrange
        var data = new ServiceData();
        var controller = new HashController(data);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        // Act
        var result = controller.Get();

        // Assert
        var content = result as ContentResult;
        Assert.IsNotNull(content, "Expected a ContentResult from Get()");
        Assert.IsTrue(content.ContentType?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) == true,
            "Expected content type text/html");
        Assert.IsTrue(!string.IsNullOrEmpty(content.Content) && content.Content.Contains("<html>"),
            "Expected returned content to contain HTML.");
    }

    [TestMethod]
    public void GetById_NotFound_Returns404()
    {
        // Arrange
        var data = new ServiceData();
        var controller = new HashController(data);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        var id = Guid.NewGuid();

        // Act
        var result = controller.GetById(id);

        // Assert
        Assert.IsInstanceOfType(result, typeof(NotFoundResult), "Expected NotFound when id does not exist.");
    }

    [TestMethod]
    public void GetById_Found_ReturnsStoredHash()
    {
        // Arrange
        var data = new ServiceData();
        var id = Guid.NewGuid();
        var bytes = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        var expectedBase64 = Convert.ToBase64String(bytes);
        var stored = new StoredHash(bytes, DateTime.UtcNow, IPAddress.Loopback);
        var added = data.TryAddHash(id, stored);
        Assert.IsTrue(added, "Precondition: failed to add stored hash.");

        var controller = new HashController(data);
        // DefaultHttpContext is fine; Request.RequestIP will fall back to Loopback
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        // Act
        var result = controller.GetById(id);

        // Assert
        var content = result as ContentResult;
        Assert.IsNotNull(content, "Expected ContentResult when hash is found.");
        Assert.AreEqual("text/plain", content.ContentType, "Expected text/plain content type.");
        Assert.AreEqual(expectedBase64, content.Content, "Returned hash string did not match stored value.");
    }

    [TestMethod]
    public async Task Put_WithValidHash_StoresAndReturns()
    {
        // Arrange
        var data = new ServiceData();
        var controller = new HashController(data);
        var ctx = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext { HttpContext = ctx };

        var id = Guid.NewGuid();
        var bytes = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();
        var base64 = Convert.ToBase64String(bytes);
        ctx.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(base64));
        ctx.Request.Body.Position = 0;

        // Act
        var action = await controller.Put(id).ConfigureAwait(false);

        // Assert action result
        var content = action as ContentResult;
        Assert.IsNotNull(content, "Expected ContentResult on successful Put.");
        Assert.AreEqual("text/plain", content.ContentType);
        Assert.AreEqual(base64, content.Content, "Put should return the stored base64 string.");

        // Assert stored in ServiceData
        var fetched = data.TryGetHash(id, IPAddress.Loopback);
        Assert.IsNotNull(fetched, "Expected hash to be stored in ServiceData.");
        Assert.AreEqual(base64, fetched!.HashAsString, "Stored hash string did not match input.");
    }

    [TestMethod]
    public async Task Put_DuplicateId_ReturnsConflict()
    {
        // Arrange
        var data = new ServiceData();
        var id = Guid.NewGuid();
        var initialBytes = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        data.TryAddHash(id, new StoredHash(initialBytes, DateTime.UtcNow, IPAddress.Loopback));

        var controller = new HashController(data);
        var ctx = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext { HttpContext = ctx };

        var newBytes = Enumerable.Repeat((byte)0xAA, 32).ToArray();
        var newBase64 = Convert.ToBase64String(newBytes);
        ctx.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(newBase64));
        ctx.Request.Body.Position = 0;

        // Act
        var action = await controller.Put(id).ConfigureAwait(false);

        // Assert
        Assert.IsInstanceOfType(action, typeof(ConflictObjectResult), "Expected Conflict when id already exists.");
        var conflict = (ConflictObjectResult)action;
        Assert.IsTrue(conflict.Value is string && ((string)conflict.Value).Contains(id.ToString()),
            "Conflict message should reference the conflicting id.");
    }

    [TestMethod]
    public async Task Put_EmptyBody_ReturnsBadRequest()
    {
        // Arrange
        var data = new ServiceData();
        var controller = new HashController(data);
        var ctx = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext { HttpContext = ctx };

        var id = Guid.NewGuid();
        ctx.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("   "));
        ctx.Request.Body.Position = 0;

        // Act
        var action = await controller.Put(id).ConfigureAwait(false);

        // Assert
        Assert.IsInstanceOfType(action, typeof(BadRequestObjectResult), "Expected BadRequest for empty body.");
        var bad = (BadRequestObjectResult)action;
        Assert.IsTrue(bad.Value is string && ((string)bad.Value).Contains("non-empty"), "BadRequest should mention non-empty body.");
    }

    [TestMethod]
    public async Task Put_InvalidBase64Length_ReturnsBadRequest()
    {
        // Arrange
        var data = new ServiceData();
        var controller = new HashController(data);
        var ctx = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext { HttpContext = ctx };

        var id = Guid.NewGuid();
        // Valid base64 but not 32 bytes after decode (e.g., 3 bytes)
        var small = Convert.ToBase64String(new byte[] { 1, 2, 3 });
        ctx.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(small));
        ctx.Request.Body.Position = 0;

        // Act
        var action = await controller.Put(id).ConfigureAwait(false);

        // Assert
        Assert.IsInstanceOfType(action, typeof(BadRequestObjectResult), "Expected BadRequest for wrong-length hash.");
        var bad = (BadRequestObjectResult)action;
        Assert.IsTrue(bad.Value is string && ((string)bad.Value).Contains("32 bytes"),
            "BadRequest should indicate expected byte length.");
    }
}
