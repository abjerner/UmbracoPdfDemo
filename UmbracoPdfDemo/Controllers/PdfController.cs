using System;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Xml;
using Fonet;
using ibex4;
using ibex4.licensing;
using Skybrud.Pdf.FormattingObjects;
using Skybrud.Pdf.FormattingObjects.Graphics;
using Skybrud.Pdf.FormattingObjects.Ibex;
using Skybrud.Pdf.FormattingObjects.Inline;
using Skybrud.Pdf.FormattingObjects.Lists;
using Skybrud.Pdf.FormattingObjects.MasterPages;
using Skybrud.Pdf.FormattingObjects.Pages;
using Skybrud.Pdf.FormattingObjects.Regions;
using Skybrud.Pdf.FormattingObjects.Styles;
using Umbraco.Core.IO;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Web;
using Umbraco.Web.WebApi;
using UmbracoPdfDemo.Models;

namespace UmbracoPdfDemo.Controllers {

    public class PdfController : UmbracoApiController {

        /// <summary>
        /// Not supposed to, I know :O
        /// </summary>
        public HttpResponse Response => HttpContext.Current.Response;

        [HttpGet]
        public void Products(string renderer = "ibex") {
            
            // Make sure to set the license key if we're using IBEX
            if (renderer == "ibex") Generator.setRuntimeKey("your license key");

            // Initialize a new document
            FoDocument document = new FoDocument();

            // Set the document properties when using IBEX
            if (renderer == "ibex") {
                document.Properties = new IbexProperties {
                    Author = "Skrift",
                    Title = "Test PDF document",
                    Subject = "Test PDF document"
                };
            }

            InitMasterPages(document);

            InitPageSequences(document, renderer, out FoFlow flow);
            
            AppendProducts(flow);

            if (renderer == "fo.net") {

                FoRenderOptions options = new FoRenderOptions {
                    UseCData = false
                };

                XmlDocument xml = document.ToXmlDocument(options);

                FonetDriver driver = FonetDriver.Make();
                Response.Clear();
                Response.ContentType = "application/pdf";
                Response.AddHeader("Content-Disposition", "filename=\"MyPdf.pdf\"");
                driver.Render(xml, Response.OutputStream);
                Response.End();
                return;

            }

            // Clear the response
            Response.Clear();

            // Set the content type and filename
            Response.ContentType = "application/pdf";
            Response.AddHeader("Content-Disposition", "filename=\"MyPdf.pdf\"");

            // Generate and output the PDF document
            using (Stream stream = document.GetStream()) {
                new FODocument().generate(stream, Response.OutputStream);
            }

            // End the response
            Response.End();

        }

        private void InitMasterPages(FoDocument document) {
            
            // Initialize the "Master" master page
            FoSimpleMasterPage master = new FoSimpleMasterPage("Master", "210mm", "297mm") {
                MarginTop = "1cm",
                MarginBottom = "0.5cm",
                MarginRight = "1.8cm",
                MarginLeft = "1.8cm"
            };

            // Add a new fo:region-body (required)
            master.Regions.Add(new FoRegionBody {
                MarginTop = "1cm",
                MarginBottom = "0cm"
            });

            // Add a new fo:region-before (optional)
            master.Regions.Add(new FoRegionBefore {
                RegionName = "header",
                Extent = "0cm",
                MarginBottom = "35px"
            });

            // Add a new fo:region-after (optional)
            master.Regions.Add(new FoRegionAfter {
                RegionName = "footer",
                Extent = "35px",
            });

            document.LayoutMasterSet.Add(master);


        }

        private void InitPageSequences(FoDocument document, string renderer, out FoFlow flow) {

            // Initialize a new flow based on the body region
            flow = new FoFlow("xsl-region-body");

            // Initialize a new page based on the flow
            FoPageSequence page = new FoPageSequence("Master", "Master", flow);
            
            // Append the page sequence to the document
            document.PageSequences.Add(page);

            // Initialize a new static content for the header (before region)
            FoStaticContent header = new FoStaticContent("header");
            page.StaticContent.Add(header);

            // Initialize a new external graphic for the logo
            FoExternalGraphic logo = new FoExternalGraphic(IOHelper.MapPath("~/images/skrift-logo.png")) {
                ContentHeight = "20px"
            };

            // Append the logo to the header
            header.Add(new FoBlock(logo) {
                TextAlign = FoTextAlign.Right
            });

            // Return if the renderer is FO.net (as fo:page-number-citation-last isn't supported)
            if (renderer == "fo.net") return;
            
            // Initialize a new static content for the footer (after region)
            FoStaticContent footer = new FoStaticContent("footer");
            page.StaticContent.Add(footer);

            // Initialize a new fo:block and append it to the footer
            FoBlock footerBlock = new FoBlock {
                FontSize = "10px",
                TextAlign = FoTextAlign.Center
            };
            footer.Add(footerBlock);

            // Append page number elements to the footer
            footerBlock.Add("Page");
            footerBlock.Add(new FoPageNumber());
            footerBlock.Add("of");
            footerBlock.Add(new FoPageNumberCitationLast {
                PageCitationStrategy = FoPageCitationStrategy.All,
                ReferenceId = page.Id
            });

        }

        private void AppendProducts(FoFlow flow) {

            IPublishedContent products = Umbraco.Content(1105);

            foreach (Product product in products.Children.OfType<Product>()) {
                AppendProduct(flow, product);
            }

        }

        private void AppendProduct(FoFlow flow, Product product) {

            // Create a new container for the contents related to a product
            FoBlock container = new FoBlock {
                FontFamily = "Arial",
                MarginBottom = "15px",
                KeepTogether = FoKeepTogether.Always
            };

            // Append the container to the flow of the page sequence
            flow.Add(container);

            // Append a block with the name to the container
            container.Add(new FoBlock(product.Name) {
                FontSize = "20px",
                FontWeight = FoFontWeight.Bold
            });

            // Append a block with the price to the container
            FoBlock price = new FoBlock { MarginTop = "5px" };
            container.Add(price);

            // Append a "Price:" label along with the actual price to the price block
            price.Add(new FoInline("Price:") { FontWeight = FoFontWeight.Bold});
            price.Add(product.Price.ToString("N0"));

            if (product.Photos != null) {

                // Get an URL for a cropped version of the photo
                string cropUrl = Request.RequestUri.GetLeftPart(UriPartial.Authority) + product.Photos.GetCropUrl(150, 100);

                // Initialize a new external graphic element
                container.Add(new FoBlock(new FoExternalGraphic(cropUrl)) {
                    MarginTop = "5px",
                });

            }

            // Append a block with the description to the container
            FoBlock description = new FoBlock(product.Description) { MarginTop = "5px" };
            container.Add(description);

            // Not all products have features
            if (product.Features.Any()) {

                // Create a new list block
                FoListBlock list = new FoListBlock();
                container.Add(list);

                // Iterate through the features
                foreach (Feature feature in product.Features) {

                    // Create both the label and body elements for the item
                    FoListItemLabel label = new FoListItemLabel();
                    FoListItemBody body = new FoListItemBody();

                    // Use a simple hyphen as the label
                    label.Add(new FoBlock("-"));

                    // We can't put the margin on an "FoListItemBody", so do it on a block instead
                    FoBlock bodyBlock = new FoBlock { MarginLeft = "15px", FontSize = "14px" };
                    body.Add(bodyBlock);

                    // Append the feature name (in bold) to the list item body
                    bodyBlock.Add(new FoBlock(feature.FeatureName) {
                        FontWeight = FoFontWeight.Bold
                    });

                    // Append the details as well
                    bodyBlock.Add(new FoBlock(feature.FeatureDetails));

                    // Now add a new list item to the list
                    list.Add(new FoListItem {
                        MarginTop = "15px",
                        MarginLeft = "15px",
                        FontSize = "14px",
                        Label = label,
                        Body = body
                    });

                }

            }

        }

    }

}