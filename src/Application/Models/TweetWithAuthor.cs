using Domain.Entities;

namespace Application.Models;

public record TweetWithAuthor(Tweet Tweet, XUserProfile? AuthorProfile);
