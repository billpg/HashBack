namespace billpg.HashBackDemoService;

public class BoudicaExceptionBase : Exception
{
    public int HttpStatus { get; }

    public BoudicaExceptionBase(int httpStatus, string message) 
        : base(message)
    {
        this.HttpStatus = httpStatus;
    }

    internal BoudicaResponse ToResponse()
    {
        throw new NotImplementedException();
    }
}

public class BadRequestException(string message) 
    : BoudicaExceptionBase(400, message) { }
