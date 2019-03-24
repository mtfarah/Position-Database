﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PD.Data;
using PD.Models;
using PD.Models.AppViewModels;
using PD.Models.AppViewModels.Filters;
using PD.Models.Compensations;
using PD.Services;

namespace PD.Controllers
{
    [Authorize(Roles = "Admin,Manager")]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ReportService _reportService;
        private readonly AppConfig _appConfig;

        public ReportsController(ApplicationDbContext context, ReportService reportService, AppConfig appConfig)
        {
            _context = context;
            _reportService = reportService;
            _appConfig = appConfig;
        }

        public IActionResult Index()
        {
            ViewBag.Departments = _context.Departments
                .OrderBy(d => d.Name)
                .ToList();

            //Getting all non-base salary component names
            List<string> components = _context.Compensations
                .Where(c => c is Adjustment)
                .Select(c => c as Adjustment)
                .Select(adj => new { adj.Name, adj.IsBaseSalaryComponent })
                .ToList()
                .Where(obj => obj.IsBaseSalaryComponent == false)
                .Select(c => c.Name)
                .Distinct()
                .ToList();
            components.Insert(0, "Base Salary");
            ViewBag.BudgetComponent = components;

            ViewBag.PositionTypes = _appConfig.FacultyTypes;

            return View();
        }

        public IActionResult People(/*PositionFilter filter*/)
        {
            //if (filter == null)
            //    filter = new PositionFilter();

            //IQueryable<PositionAssignment> positionAssignments = _reportService.GetPositionAssignments(filter);

            //ViewBag.Filter = filter;
            //ViewBag.DataProtector = _reportService._dataProtector;

            return View();
        }

        public IActionResult Positions(PositionFilter filter)
        {
            if (filter == null)
                filter = new PositionFilter();

            IQueryable<PositionAssignment> positionAssignments = _reportService.GetPositionAssignments(filter);

            ViewBag.Filter = filter;
            ViewBag.DataProtector = _reportService._dataProtector;

            return View("PositionAssignments", positionAssignments);
            //return View(positionAssignments.ToList().Select(pp => new PositionViewModel(pp, filter.Date, srv.DataProtector)));
        }

        public IActionResult Details(int id, DateTime targetDate)
        {
            PositionAssignment pa = _reportService.GetPositionAssignment(id, targetDate, true, true, true, true);

            ViewBag.Filter = new PositionFilter() { Date = targetDate };
            ViewBag.DataProtector = _reportService._dataProtector;

            return View(pa);
        }
    }
}

