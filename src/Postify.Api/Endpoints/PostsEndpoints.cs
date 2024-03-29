﻿using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Postify.Abstractions;
using Postify.Abstractions.Infrastructure;
using Postify.Abstractions.Persistence;
using Postify.Infrastructure;
using Postify.Requests;
using Postify.Responses;
using Postify.Validation;

namespace Postify.Endpoints;

public static class PostsEndpoints
{
    public static void MapPostEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(Apis.Posts.Route);

        group
            .MapPost($"/{Apis.Posts.Endpoints.Create}", CreatePostAsync)
            .RequireAuthorization()
            .WithName(Apis.Posts.Endpoints.Create)
            .WithOpenApi();

        group
            .MapGet($"/{Apis.Posts.Endpoints.GetAll}", GetAllPosts)
            .RequireAuthorization()
            .WithName(Apis.Posts.Endpoints.GetAll)
            .WithOpenApi();

        group
            .MapPut($"/{Apis.Posts.Endpoints.Update}", UpdatePostAsync)
            .RequireAuthorization()
            .WithName(Apis.Posts.Endpoints.Update)
            .WithOpenApi();

        group
            .MapDelete($"/{Apis.Posts.Endpoints.Delete}", DeletePostAsync)
            .RequireAuthorization()
            .WithName(Apis.Posts.Endpoints.Delete)
            .WithOpenApi();

        group
            .MapPut($"/{Apis.Posts.Endpoints.SetPostStatus}", SetPostStatusAsync)
            .RequireAuthorization(AuthorizationPolicies.AdminUser)
            .WithName(Apis.Posts.Endpoints.SetPostStatus)
            .WithOpenApi();
    }

    private static async Task<IResult> CreatePostAsync(
        [FromBody] PostInfo postInfo,
        IValidator<PostInfo> validator,
        IPostRepository postRepository,
        HttpContext httpContext)
    {
        var validationErrors = validator
            .Validate(postInfo)
            .ToErrorMessages();

        if (validationErrors.Length != 0)
            return TypedResults.BadRequest<FailureResult>(validationErrors);

        var authenticatedUserId = httpContext.GetAuthenticatedUserId();

        var addedPost = await postRepository.AddAsync(
            postInfo.Subject,
            postInfo.Content,
            authenticatedUserId);

        return TypedResults.Ok<SuccessResult<Guid>>(addedPost.Id);
    }

    private static async Task<IResult> GetAllPosts(
        IPostRepository postRepository,
        HttpContext httpContext)
    {
        var authenticatedUserId = httpContext.GetAuthenticatedUserId();

        var posts = await postRepository
            .GetAllByUserId(authenticatedUserId)
            .Select(p => p.ToDto())
            .ToListAsync();

        return TypedResults.Ok<SuccessResult<List<PostDto>>>(posts);
    }

    private static async Task<IResult> UpdatePostAsync(
        [FromQuery] Guid postId,
        [FromBody] PostInfo newPostInfo,
        IValidator<PostInfo> validator,
        IPostRepository postRepository)
    {
        var validationErrors = validator
            .Validate(newPostInfo)
            .ToErrorMessages();

        if (validationErrors.Length != 0)
            return TypedResults.BadRequest<FailureResult>(validationErrors);

        var post = await postRepository.GetByIdAsync(postId);

        if (post is null)
            return TypedResults.NotFound<FailureResult>(ResponseMessages.PostNotFound);

        post.Subject = newPostInfo.Subject;
        post.Content = newPostInfo.Content;

        await postRepository.UpdateAsync(post);

        return TypedResults.Ok<SuccessResult>(ResponseMessages.PostUpdatedSuccessfully);
    }

    private static async Task<IResult> DeletePostAsync(
        [FromQuery] Guid postId,
        IPostRepository postRepository)
    {
        var post = await postRepository.GetByIdAsync(postId);

        if (post is null)
            return TypedResults.NotFound<FailureResult>(ResponseMessages.PostNotFound);

        await postRepository.DeleteAsync(post);

        return TypedResults.Ok<SuccessResult>(ResponseMessages.PostDeletedSuccessfully);
    }

    private static async Task<IResult> SetPostStatusAsync(
        [FromQuery] Guid postId,
        [FromQuery] bool status,
        IPostRepository postRepository)
    {
        var post = await postRepository.GetByIdAsync(postId);

        if (post is null)
            return TypedResults.NotFound<FailureResult>(ResponseMessages.PostNotFound);

        await postRepository.SetPostStatusAsync(post, status);

        return TypedResults.Ok<SuccessResult>(ResponseMessages.PostStatusModifiedSuccessfully);
    }
}
